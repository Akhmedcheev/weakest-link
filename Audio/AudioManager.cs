using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace WeakestLink.Audio
{
    /// <summary>
    /// Простой менеджер аудио для воспроизведения игровых звуков и фоновых подложек.
    /// </summary>
    public class AudioManager : IDisposable
    {
        private WaveOutEvent? _outputDevice;
        private AudioFileReader? _audioFile;
        private WaveOutEvent? _oneShotDevice;
        private AudioFileReader? _oneShotReader;
        private WaveOutEvent? _crossfadeBedDevice;
        private AudioFileReader? _crossfadeBedReader;
        private LoopStream? _crossfadeBedLoop;
        private readonly object _oneShotLock = new object();
        private bool _mainStopRequested;

        /// <summary>
        /// Вызывается, когда основной канал доиграл до конца (не при Stop).
        /// Обнуляется при Stop или при срабатывании.
        /// </summary>
        public Action? OnMainPlaybackCompleted { get; set; }

        /// <summary>
        /// Воспроизводит один трек без зацикливания и возвращает Task, завершающийся по окончании воспроизведения.
        /// Не блокирует поток. Текущее воспроизведение останавливается перед стартом.
        /// </summary>
        /// <param name="filePath">Путь к файлу относительно корня приложения.</param>
        /// <returns>Task, завершающийся когда трек доигран или произошла ошибка.</returns>
        public Task PlayOneShotAsync(string filePath)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                string fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

                if (!File.Exists(fullPath))
                {
                    tcs.TrySetResult();
                    return tcs.Task;
                }

                Stop();

                var device = new WaveOutEvent();
                var reader = new AudioFileReader(fullPath);

                lock (_oneShotLock)
                {
                    _oneShotDevice = device;
                    _oneShotReader = reader;
                }

                void OnPlaybackStopped(object? sender, StoppedEventArgs e)
                {
                    device.PlaybackStopped -= OnPlaybackStopped;
                    lock (_oneShotLock)
                    {
                        if (_oneShotDevice == device) { _oneShotDevice = null; _oneShotReader = null; }
                    }
                    try
                    {
                        device.Dispose();
                        reader.Dispose();
                    }
                    catch { /* ignore */ }
                    tcs.TrySetResult();
                }

                device.PlaybackStopped += OnPlaybackStopped;
                device.Init(reader);
                device.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayOneShotAsync error: {ex.Message}");
                tcs.TrySetResult();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Проигрывает звуковой файл. Если что-то уже играет, останавливает и заменяет.
        /// </summary>
        /// <param name="filePath">Путь к файлу относительно корня приложения.</param>
        /// <param name="loop">Нужно ли зацикливать воспроизведение.</param>
        /// <param name="startFromSeconds">Если > 0, начинает воспроизведение с указанной секунды.</param>
        public void Play(string filePath, bool loop = false, double startFromSeconds = 0)
        {
            try
            {
                string fullPath = Path.IsPathRooted(filePath) 
                    ? filePath 
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

                if (!File.Exists(fullPath)) return;

                Stop();

                _outputDevice = new WaveOutEvent();
                _audioFile = new AudioFileReader(fullPath);

                if (loop)
                {
                    var loopStream = new LoopStream(_audioFile);
                    _outputDevice.Init(loopStream);
                }
                else
                {
                    _outputDevice.Init(_audioFile);
                }

                if (startFromSeconds > 0 && _audioFile.TotalTime.TotalSeconds > startFromSeconds)
                {
                    _audioFile.CurrentTime = TimeSpan.FromSeconds(startFromSeconds);
                }

                _mainStopRequested = false;
                _outputDevice.PlaybackStopped += MainDevice_PlaybackStopped;
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
            }
        }

        private void MainDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= MainDevice_PlaybackStopped;
            }
            if (_mainStopRequested) return;
            var cb = OnMainPlaybackCompleted;
            OnMainPlaybackCompleted = null;
            cb?.Invoke();
        }

        /// <summary>
        /// Останавливает текущее воспроизведение и освобождает ресурсы (основной канал и one-shot).
        /// </summary>
        public void Stop()
        {
            _mainStopRequested = true;
            OnMainPlaybackCompleted = null;
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _outputDevice = null;
            _audioFile?.Dispose();
            _audioFile = null;

            if (_crossfadeBedDevice != null)
            {
                try
                {
                    _crossfadeBedDevice.Stop();
                    _crossfadeBedDevice.Dispose();
                    _crossfadeBedLoop?.Dispose();
                    _crossfadeBedReader?.Dispose();
                }
                catch { /* ignore */ }
                _crossfadeBedDevice = null;
                _crossfadeBedLoop = null;
                _crossfadeBedReader = null;
            }

            lock (_oneShotLock)
            {
                if (_oneShotDevice != null)
                {
                    try
                    {
                        _oneShotDevice.Stop();
                        _oneShotDevice.Dispose();
                        _oneShotReader?.Dispose();
                    }
                    catch { /* ignore */ }
                    _oneShotDevice = null;
                    _oneShotReader = null;
                }
            }
        }

        /// <summary>
        /// Воспроизводит фоновую подложку (bed). Путь — относительно Assets/Audio/.
        /// </summary>
        public void PlayBed(string fileName, bool loop = true)
        {
            string path = Path.IsPathRooted(fileName) || fileName.StartsWith("Assets")
                ? fileName
                : Path.Combine("Assets", "Audio", fileName);
            Play(path, loop);
        }

        /// <summary>
        /// Воспроизводит системный звук (голос диктора и т.п.) — один раз. Путь — относительно Assets/Audio/.
        /// </summary>
        public void PlaySystem(string fileName)
        {
            string path = Path.IsPathRooted(fileName) || fileName.StartsWith("Assets")
                ? fileName
                : Path.Combine("Assets", "Audio", fileName);
            Play(path, loop: false);
        }

        /// <summary>
        /// Воспроизводит bed-файл как один трек и возвращает Task, завершающийся по окончании.
        /// </summary>
        public Task PlayBedOneShotAsync(string fileName)
        {
            string path = Path.IsPathRooted(fileName) || fileName.StartsWith("Assets")
                ? fileName
                : Path.Combine("Assets", "Audio", fileName);
            return PlayOneShotAsync(path);
        }

        /// <summary>
        /// Воспроизводит one-shot трек, а за 2–3 сек до его окончания запускает bed (general_bed.mp3)
        /// с плавным нарастанием громкости (кроссфейд).
        /// </summary>
        public Task PlayOneShotThenGeneralBedWithCrossfadeAsync(string oneShotFileName, string bedFileName = "general_bed.mp3", double crossfadeSeconds = 2.5)
        {
            string oneShotPath = Path.IsPathRooted(oneShotFileName) || oneShotFileName.StartsWith("Assets")
                ? oneShotFileName
                : Path.Combine("Assets", "Audio", oneShotFileName);
            string bedPath = Path.IsPathRooted(bedFileName) || bedFileName.StartsWith("Assets")
                ? bedFileName
                : Path.Combine("Assets", "Audio", bedFileName);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                string oneShotFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, oneShotPath);
                string bedFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bedPath);

                if (!File.Exists(oneShotFullPath))
                {
                    tcs.TrySetResult();
                    return tcs.Task;
                }

                Stop();

                var device = new WaveOutEvent();
                var reader = new AudioFileReader(oneShotFullPath);
                double durationSeconds = reader.TotalTime.TotalSeconds;
                double crossfadeStart = Math.Max(0, durationSeconds - crossfadeSeconds);

                lock (_oneShotLock)
                {
                    _oneShotDevice = device;
                    _oneShotReader = reader;
                }

                void OnPlaybackStopped(object? sender, StoppedEventArgs e)
                {
                    device.PlaybackStopped -= OnPlaybackStopped;
                    lock (_oneShotLock)
                    {
                        if (_oneShotDevice == device) { _oneShotDevice = null; _oneShotReader = null; }
                    }
                    try
                    {
                        device.Dispose();
                        reader.Dispose();
                    }
                    catch { /* ignore */ }
                    tcs.TrySetResult();
                }

                device.PlaybackStopped += OnPlaybackStopped;
                device.Init(reader);
                device.Play();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        int delayMs = (int)(crossfadeStart * 1000);
                        if (delayMs > 0)
                            await Task.Delay(delayMs);

                        if (!File.Exists(bedFullPath)) return;

                        lock (_oneShotLock)
                        {
                            if (_oneShotDevice == null) return;
                        }

                        var bedReader = new AudioFileReader(bedFullPath) { Volume = 0f };
                        var loop = new LoopStream(bedReader);
                        var bedDevice = new WaveOutEvent();
                        bedDevice.Init(loop);
                        bedDevice.Play();

                        _crossfadeBedDevice = bedDevice;
                        _crossfadeBedReader = bedReader;
                        _crossfadeBedLoop = loop;

                        int rampSteps = (int)(crossfadeSeconds * 20);
                        float step = 1f / rampSteps;
                        int stepMs = (int)(crossfadeSeconds * 1000 / rampSteps);

                        for (int i = 1; i <= rampSteps; i++)
                        {
                            await Task.Delay(stepMs);
                            if (_crossfadeBedReader == null) break;
                            float v = Math.Min(1f, i * step);
                            try { _crossfadeBedReader.Volume = v; } catch { break; }
                        }

                        if (_crossfadeBedReader != null)
                            _crossfadeBedReader.Volume = 1f;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Crossfade error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayOneShotThenGeneralBedWithCrossfade error: {ex.Message}");
                tcs.TrySetResult();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Запускает bed-трек с fade-in поверх текущего воспроизведения (без Stop).
        /// Основной трек продолжает играть и завершится естественно.
        /// После fade-in bed становится основным каналом.
        /// </summary>
        public void StartBedWithFadeIn(string bedFileName = "general_bed.mp3", double fadeSeconds = 3.0)
        {
            string bedPath = Path.IsPathRooted(bedFileName) || bedFileName.StartsWith("Assets")
                ? bedFileName
                : Path.Combine("Assets", "Audio", bedFileName);
            string bedFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bedPath);

            if (!File.Exists(bedFullPath)) return;

            try
            {
                var bedReader = new AudioFileReader(bedFullPath) { Volume = 0f };
                var loop = new LoopStream(bedReader);
                var bedDevice = new WaveOutEvent();
                bedDevice.Init(loop);
                bedDevice.Play();

                _crossfadeBedDevice = bedDevice;
                _crossfadeBedReader = bedReader;
                _crossfadeBedLoop = loop;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        int rampSteps = (int)(fadeSeconds * 20);
                        float step = 1f / rampSteps;
                        int stepMs = (int)(fadeSeconds * 1000 / rampSteps);

                        for (int i = 1; i <= rampSteps; i++)
                        {
                            await Task.Delay(stepMs);
                            if (_crossfadeBedReader == null) break;
                            float v = Math.Min(1f, i * step);
                            try { _crossfadeBedReader.Volume = v; } catch { break; }
                        }

                        if (_crossfadeBedReader != null)
                            _crossfadeBedReader.Volume = 1f;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"StartBedWithFadeIn error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartBedWithFadeIn init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает текущую позицию и длительность основного аудио-канала.
        /// Если ничего не играет, оба значения = TimeSpan.Zero.
        /// </summary>
        public (TimeSpan position, TimeSpan duration) GetAudioPosition()
        {
            try
            {
                var file = _audioFile;
                if (file != null)
                    return (file.CurrentTime, file.TotalTime);
            }
            catch { }
            return (TimeSpan.Zero, TimeSpan.Zero);
        }

        /// <summary>
        /// Принудительно останавливает все звуки (алиас для Stop).
        /// </summary>
        public void StopAll()
        {
            Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
