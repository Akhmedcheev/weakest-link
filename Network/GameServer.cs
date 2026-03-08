using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WeakestLink.Network
{
    /// <summary>
    /// TCP Сервер для рассылки игровых данных всем окнам-клиентам.
    /// </summary>
    public class GameServer
    {
        private readonly int _port;
        private int _boundPort;
        private TcpListener? _listener;

        /// <summary>Порт, на котором сервер фактически слушает (может отличаться от запрошенного, если тот занят).</summary>
        public int BoundPort => _boundPort;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private bool _isRunning;

        public GameServer(int port = 8888)
        {
            _port = port;
        }

        public void Start()
        {
            int portToUse = _port;
            var portsToTry = new[] { _port, 8889, 8890, 8891, 8892 };
            Exception? lastEx = null;

            foreach (int p in portsToTry)
            {
                try
                {
                    _isRunning = true;
                    _listener = new TcpListener(IPAddress.Any, p);
                    _listener.Start();
                    _boundPort = p;
                    break;
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    lastEx = ex;
                    _listener = null;
                }
            }

            if (_listener == null)
                throw new InvalidOperationException($"Не удалось запустить TCP-сервер. Порты {string.Join(", ", portsToTry)} заняты.", lastEx);

            // Фоновый поток для принятия подключений
            Task.Run(async () =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        lock (_clients)
                        {
                            _clients.Add(client);
                        }
                        // Можно добавить логику обработки входящих данных от клиента здесь, 
                        // но для нашей задачи сервер преимущественно вещательный.
                        _ = HandleClientAsync(client);
                    }
                    catch
                    {
                        if (!_isRunning) break;
                    }
                }
            });
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                // Просто держим соединение открытым. Если клиент отключится - удалим из списка.
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];
                while (_isRunning && client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break; // Отключение
                }
            }
            catch
            {
                // Ошибка чтения
            }
            finally
            {
                lock (_clients)
                {
                    _clients.Remove(client);
                }
                client.Close();
            }
        }

        /// <summary>
        /// Рассылка сообщения всем подключенным клиентам.
        /// </summary>
        public void Broadcast(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            lock (_clients)
            {
                foreach (var client in _clients.ToArray())
                {
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            _clients.Remove(client);
                        }
                    }
                    catch
                    {
                        _clients.Remove(client);
                    }
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            lock (_clients)
            {
                foreach (var client in _clients) client.Close();
                _clients.Clear();
            }
        }
    }
}
