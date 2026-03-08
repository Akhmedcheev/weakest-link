using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WeakestLink.Network
{
    /// <summary>
    /// TCP Клиент для получения команд от сервера (пульта оператора).
    /// </summary>
    public class GameClient
    {
        private readonly string _ip;
        private readonly int _port;
        private TcpClient? _client;
        private bool _isRunning;

        public event Action<string>? MessageReceived;
        public event Action? Connected;
        public event Action? Disconnected;

        public GameClient(string ip = "127.0.0.1", int port = 8888)
        {
            _ip = ip;
            _port = port;
        }

        public void Start()
        {
            _isRunning = true;
            _ = ConnectLoopAsync();
        }

        private async Task ConnectLoopAsync()
        {
            while (_isRunning)
            {
                if (_client == null || !_client.Connected)
                {
                    try
                    {
                        _client = new TcpClient();
                        await _client.ConnectAsync(_ip, _port);
                        Connected?.Invoke();
                        _ = ListenAsync();
                    }
                    catch
                    {
                        // Ошибка подключения - подождем 3 секунды и попробуем снова
                        await Task.Delay(3000);
                        continue;
                    }
                }
                await Task.Delay(1000);
            }
        }

        private async Task ListenAsync()
        {
            if (_client == null) return;

            try
            {
                using var reader = new StreamReader(_client.GetStream(), Encoding.UTF8);
                while (_isRunning && _client.Connected)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break; // Сервер отключился
                    
                    line = line?.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        MessageReceived?.Invoke(line);
                    }
                }
            }
            catch
            {
                // Ошибка чтения или разрыв
            }
            finally
            {
                Disconnected?.Invoke();
                _client?.Close();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _client?.Close();
        }
    }
}
