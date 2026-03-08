using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WeakestLink.Network
{
    public class WebRemoteController
    {
        private readonly HttpListener _listener;
        private readonly Action<string> _commandCallback;
        private readonly Action<string> _logCallback;
        private readonly Func<object> _stateProvider;
        private readonly Func<object>? _hostStateProvider;
        private CancellationTokenSource? _cts;

        public WebRemoteController(string localIp, int port, Action<string> commandCallback, Action<string> logCallback, Func<object> stateProvider, Func<object>? hostStateProvider = null)
        {
            _listener = new HttpListener();
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            _commandCallback = commandCallback;
            _logCallback = logCallback;
            _stateProvider = stateProvider;
            _hostStateProvider = hostStateProvider;

            _localIp = localIp;
            _port = port;
        }

        private string _localIp;
        private int _port;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web_remote_status.txt");
            
            try 
            {
                _listener.Prefixes.Clear();
                
                // Prioritize the specific port requested
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                
                if (!string.IsNullOrEmpty(_localIp) && _localIp != "127.0.0.1")
                {
                    _listener.Prefixes.Add($"http://{_localIp}:{_port}/");
                }

                // Try wildcard but don't fail if it requires admin
                try {
                    _listener.Prefixes.Add($"http://+:{_port}/");
                } catch {
                    _logCallback?.Invoke("WebRemote: Wildcard prefix (+) skipped (requires Admin).");
                }

                _listener.Start();
                
                _logCallback?.Invoke($"Web Remote сервер успешно запущен на порту {_port}.");
                _logCallback?.Invoke($"Привязанные адреса: {string.Join(", ", _listener.Prefixes)}");
                System.IO.File.WriteAllText(logPath, $"SUCCESS: Listening on {string.Join(", ", _listener.Prefixes)} at {DateTime.Now}");
                Task.Run(() => ListenLoop(_cts.Token));
            } 
            catch (Exception ex) 
            {
                _logCallback?.Invoke($"ОШИБКА ЗАПУСКА Web Remote: {ex.Message}");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL: Start failed: {ex.Message}\n");
                throw;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            if (_listener.IsListening)
                _listener.Stop();
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequest(context);
                }
                catch (Exception)
                {
                    if (token.IsCancellationRequested) break;
                    // Minimal internal logging for loop errors
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string url = request.Url?.AbsolutePath ?? "/";
                // Log all requests for debugging
                System.IO.File.AppendAllText("web_remote_requests.txt", $"[{DateTime.Now}] {request.HttpMethod} {url} from {request.RemoteEndPoint}\n");

                if (url == "/" || string.IsNullOrEmpty(url) || url.ToLower() == "/index.html")
                {
                    response.StatusCode = 200;
                    string html = GetHtmlTemplate();
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;

                    if (request.HttpMethod != "HEAD")
                    {
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                else if (url == "/host" || url == "/host/")
                {
                    response.StatusCode = 200;
                    string html = GetHostHtmlTemplate();
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    if (request.HttpMethod != "HEAD")
                    {
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                else if (url == "/api/host-state")
                {
                    response.StatusCode = 200;
                    var hostState = _hostStateProvider?.Invoke() ?? new { state = "Idle" };
                    string json = System.Text.Json.JsonSerializer.Serialize(hostState);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (url == "/api/state")
                {
                    response.StatusCode = 200;
                    var state = _stateProvider?.Invoke() ?? new { question = "-", timer = "00:00" };
                    string json = System.Text.Json.JsonSerializer.Serialize(state);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (url == "/test")
                {
                    response.StatusCode = 200;
                    byte[] buffer = Encoding.UTF8.GetBytes("Web Remote is Working!");
                    response.ContentType = "text/plain";
                    response.ContentLength64 = buffer.Length;

                    if (request.HttpMethod != "HEAD")
                    {
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                else if (url.StartsWith("/api/command"))
                {
                    string action = request.QueryString["action"] ?? "";
                    if (!string.IsNullOrEmpty(action))
                    {
                        // Получаем текущую активность
                        bool isActiveNow = true;
                        var rawState = _stateProvider?.Invoke();
                        if (rawState != null)
                        {
                            // Отражаем isActive из анонимного объекта через рефлексию или просто передаем действие
                            var prop = rawState.GetType().GetProperty("isActive");
                            if (prop != null)
                                isActiveNow = (bool)prop.GetValue(rawState)!;
                        }

                        // Если не Playing/FinalDuel, игнорируем основные игровые кнопки
                        string[] restricted = { "BANK", "CORRECT", "WRONG", "PASS" };
                        if (!isActiveNow && restricted.Contains(action.ToUpper()))
                        {
                            response.StatusCode = 403; // Forbidden in current state
                            byte[] forbiddenBuffer = Encoding.UTF8.GetBytes("IGNORED_IN_WAIT_STATE");
                            await response.OutputStream.WriteAsync(forbiddenBuffer, 0, forbiddenBuffer.Length);
                            return;
                        }

                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() => _commandCallback(action.ToUpper()));
                        }
                    }

                    response.StatusCode = 200;
                    string result = "OK";
                    byte[] buffer = Encoding.UTF8.GetBytes(result);
                    response.ContentType = "text/plain";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                System.IO.File.AppendAllText("web_remote_error.txt", $"[{DateTime.Now}] HANDLE ERROR: {ex.Message}\n{ex.StackTrace}\n");
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private string GetHtmlTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <title>WL iPad Remote</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #050510; color: #fff; margin: 0; padding: 15px; display: flex; flex-direction: column; align-items: center; min-height: 100vh; }
        
        .state-container { width: 100%; max-width: 700px; background: rgba(255,255,255,0.05); border-radius: 20px; padding: 20px; margin-bottom: 20px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); text-align: center; border: 1px solid rgba(255,255,255,0.1); }
        .timer { font-size: 80px; font-weight: bold; font-family: 'Courier New', monospace; color: #ffcc00; text-shadow: 0 0 20px rgba(255,204,0,0.4); margin: 0; line-height: 1; }
        .bank-info { font-size: 24px; font-weight: bold; color: #FFD700; margin-top: 5px; text-transform: uppercase; letter-spacing: 2px; }
        .question { font-size: 24px; margin-top: 15px; color: #e0e0e0; min-height: 80px; display: flex; align-items: center; justify-content: center; line-height: 1.3; }
        .answer { font-size: 28px; font-weight: bold; background: #C10000; color: white; padding: 10px 20px; border-radius: 10px; margin-top: 15px; box-shadow: 0 5px 15px rgba(193,0,0,0.3); }

        .wait-screen { display: none; flex-direction: column; align-items: center; justify-content: center; padding: 50px 20px; text-align: center; color: #666; font-size: 28px; font-weight: bold; text-transform: uppercase; letter-spacing: 3px; border: 2px dashed #222; border-radius: 20px; width: 100%; max-width: 700px; }

        .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; width: 100%; max-width: 700px; }
        .btn { border: none; font-size: 24px; font-weight: bold; color: white; padding: 35px 15px; border-radius: 15px; text-align: center; cursor: pointer; user-select: none; transition: 0.1s; display: flex; align-items: center; justify-content: center; }
        .btn:active { transform: scale(0.96); opacity: 0.9; }
        
        .btn-correct { background: linear-gradient(145deg, #008800, #004400); box-shadow: 0 5px 15px rgba(0,100,0,0.3); }
        .btn-wrong { background: linear-gradient(145deg, #cc0000, #880000); box-shadow: 0 5px 15px rgba(100,0,0,0.3); }
        .btn-pass { background: linear-gradient(145deg, #555, #333); }
        .btn-next { background: linear-gradient(145deg, #0066cc, #003366); box-shadow: 0 5px 15px rgba(0,50,150,0.3); }
        .btn-bank { background: linear-gradient(145deg, #ffcc00, #ccaa00); grid-column: span 2; padding: 45px 15px; font-size: 45px; color: #000; text-shadow: 0 1px 2px rgba(255,255,255,0.5); margin-bottom: 5px; }
        
        .status-bar { margin-top: auto; padding-top: 20px; color: #444; font-size: 13px; text-transform: uppercase; letter-spacing: 1px; }
    </style>
</head>
<body>
    <div class='state-container'>
        <div class='timer' id='timer-val'>0:00</div>
        <div class='bank-info' id='bank-val'>СУММА: 0 ₽</div>
        <div class='question' id='question-text'>Ожидание начала раунда...</div>
        <div class='answer' id='answer-val'>—</div>
    </div>

    <div id='wait-msg' class='wait-screen'>
        ОЖИДАНИЕ РАУНДА / ГОЛОСОВАНИЕ
    </div>

    <div id='controls-grid' class='grid'>
        <button class='btn btn-BANK' onclick='send(""BANK"")'>БАНК!</button>
        <button class='btn btn-CORRECT' onclick='send(""CORRECT"")'>ВЕРНО</button>
        <button class='btn btn-WRONG' onclick='send(""WRONG"")'>НЕВЕРНО</button>
        <button class='btn btn-PASS' onclick='send(""PASS"")'>ПАС</button>
        <button class='btn btn-NEXT' onclick='send(""NEXT"")'>NEXT</button>
    </div>

    <div class='status-bar'>Weakest Link iPad Remote • v1.4 Live</div>

    <script>
        const qEl = document.getElementById('question-text');
        const tEl = document.getElementById('timer-val');
        const aEl = document.getElementById('answer-val');
        const bEl = document.getElementById('bank-val');
        const waitEl = document.getElementById('wait-msg');
        const gridEl = document.getElementById('controls-grid');

        async function updateState() {
            try {
                const r = await fetch('/api/state');
                const state = await r.json();
                
                // Обновление текстов
                if (state.question !== qEl.innerText) qEl.innerText = state.question || '—';
                if (state.timer !== tEl.innerText) tEl.innerText = state.timer || '0:00';
                if (state.answer !== aEl.innerText) aEl.innerText = state.answer || '—';
                const fBank = 'СУММА: ' + (state.bank || '0') + ' ₽';
                if (fBank !== bEl.innerText) bEl.innerText = fBank;

                // Управление видимостью кнопок
                if (state.isActive) {
                    gridEl.style.display = 'grid';
                    waitEl.style.display = 'none';
                } else {
                    gridEl.style.display = 'none';
                    waitEl.style.display = 'flex';
                }
            } catch (e) { console.error('State poll failed', e); }
        }

        function send(action) {
            fetch('/api/command?action=' + action).catch(console.error);
        }

        setInterval(updateState, 1000);
        updateState();
    </script>
</body>
</html>";
        }

        private string GetHostHtmlTemplate()
        {
            return @"
<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover'>
    <title>Weakest Link — Экран ведущего</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        html, body { height: 100%; width: 100%; overflow: hidden; background: #050510; color: white; font-family: 'Segoe UI', Tahoma, sans-serif; }
        #root { display: flex; width: 100vw; height: 100vh; min-height: 100dvh; max-height: 100dvh; }
        /* Левая колонка: денежная цепочка */
        #money-tree { width: 18vw; min-width: 120px; max-width: 280px; background: #020208; border-right: 1px solid #111; display: flex; flex-direction: column; flex-shrink: 0; }
        #money-tree.hidden { display: none !important; }
        .chain-title { text-align: center; font-size: clamp(12px, 2.5vw, 18px); font-weight: 600; color: #444; padding: 1.5vh 0 1vh; }
        #chain-list { flex: 1; display: flex; flex-direction: column; justify-content: space-evenly; align-items: center; gap: 2px; overflow: hidden; }
        .chain-item { width: 70%; max-width: 160px; height: clamp(36px, 5.5vh, 58px); border-radius: 50%; background: #001530; border: 1px solid #222; display: flex; align-items: center; justify-content: center; font-size: clamp(14px, 2.5vw, 28px); font-weight: 800; color: #888; flex-shrink: 0; }
        .chain-item.active { background: #C10000; border: 2px solid white; color: white; font-size: clamp(16px, 2.8vw, 32px); box-shadow: 0 0 25px rgba(255,0,0,0.5); }
        .bank-cell { margin: 1vh auto 3vh; width: 80%; max-width: 150px; height: clamp(40px, 5vh, 55px); border-radius: 50%; background: #C10000; border: 2px solid white; display: flex; align-items: center; justify-content: center; font-size: clamp(18px, 2.8vw, 32px); font-weight: 900; }
        .bank-label { text-align: center; font-size: clamp(12px, 1.8vw, 18px); font-weight: 900; color: #555; }
        /* Центральная область */
        #main { flex: 1; display: flex; flex-direction: column; margin: 2vw; min-width: 0; }
        #header { font-size: clamp(24px, 4vw, 48px); font-weight: 800; color: #FFFF00; margin-bottom: 1vh; font-family: 'Segoe UI Semibold', sans-serif; }
        #header.final { color: white; }
        #question-block { flex: 1; background: #0A1128; border: 2px solid #1A2850; padding: 2.5vw; display: flex; align-items: center; justify-content: center; min-height: 20vh; }
        #question-text { font-size: clamp(28px, 6vw, 88px); font-weight: bold; color: white; text-align: center; line-height: 1.2; word-wrap: break-word; }
        #answer-block { background: #C10000; padding: 2vh 2.5vw; margin-top: 1vh; }
        #answer-text { font-size: clamp(24px, 5vw, 78px); font-weight: bold; color: white; text-align: center; word-wrap: break-word; }
        /* Нижняя панель 2x4 */
        #bottom-panel { display: grid; grid-template-columns: auto 1fr auto 1fr; grid-template-rows: 1fr 1fr; gap: 5px; margin-top: 2vh; flex: 0 0 auto; min-height: 14vh; }
        #bottom-panel.hidden { display: none !important; }
        .panel-label { font-size: clamp(16px, 2.8vw, 36px); font-weight: 600; color: #888; display: flex; align-items: center; padding-right: 1vw; }
        .panel-value { background: #0000B8; margin: 5px; display: flex; align-items: center; justify-content: center; font-size: clamp(20px, 5vw, 72px); font-weight: 800; color: white; }
        .panel-value.purple { background: #800080; }
        .panel-value.green { background: #006400; }
        .panel-value.dark { background: #4A0020; }
        /* Панель финальной дуэли */
        #final-duel { display: none; grid-template-columns: 1fr auto 1fr; gap: 2vw; align-items: center; justify-items: center; margin-top: 2vh; flex: 0 0 auto; min-height: 18vh; background: #050510; }
        #final-duel.visible { display: grid !important; }
        .finalist-name { font-size: clamp(22px, 4vw, 50px); font-weight: bold; color: white; text-align: center; margin-bottom: 8px; }
        .finalist-dots { display: flex; flex-direction: row; justify-content: center; gap: 8px; flex-wrap: wrap; }
        .dot { width: clamp(24px, 4vw, 35px); height: clamp(24px, 4vw, 35px); border-radius: 50%; background: #222; border: 1px solid #444; }
        .dot.correct { background: green; border-color: #0a0; }
        .dot.wrong { background: red; border-color: #a00; }
        #vs { font-size: clamp(32px, 5vw, 60px); font-weight: 900; color: #444; }
    </style>
</head>
<body>
    <div id='root'>
        <aside id='money-tree'>
            <div class='chain-title'>ЦЕПОЧКА</div>
            <div id='chain-list'></div>
            <div class='bank-cell'><span id='bank-zero'>0</span></div>
            <div class='bank-label'>BANK</div>
        </aside>
        <main id='main'>
            <div id='header'>1000 ВОПРОС : 1</div>
            <div id='question-block'><div id='question-text'>ОЖИДАНИЕ ВОПРОСА ДЛЯ ВЕДУЩЕГО...</div></div>
            <div id='answer-block'><div id='answer-text'>ПРАВИЛЬНЫЙ ОТВЕТ</div></div>
            <div id='bottom-panel'>
                <span class='panel-label'>В БАНК</span>
                <div class='panel-value'><span id='to-bank'>1000</span></div>
                <span class='panel-label'>ИЛИ ИГРАЕМ</span>
                <div class='panel-value purple'><span id='next-sum'>2000</span></div>
                <span class='panel-label'>ТАЙМЕР</span>
                <div class='panel-value green'><span id='timer'>2:30</span></div>
                <span class='panel-label'>В БАНКЕ</span>
                <div class='panel-value dark'><span id='banked'>0</span></div>
            </div>
            <div id='final-duel'>
                <div class='finalist-col'>
                    <div class='finalist-name' id='finalist1'>ФИНАЛИСТ 1</div>
                    <div class='finalist-dots' id='dots1'></div>
                </div>
                <div id='vs'>VS</div>
                <div class='finalist-col'>
                    <div class='finalist-name' id='finalist2'>ФИНАЛИСТ 2</div>
                    <div class='finalist-dots' id='dots2'></div>
                </div>
            </div>
        </main>
    </div>
    <script>
        const header = document.getElementById('header');
        const questionText = document.getElementById('question-text');
        const answerText = document.getElementById('answer-text');
        const chainList = document.getElementById('chain-list');
        const moneyTree = document.getElementById('money-tree');
        const bottomPanel = document.getElementById('bottom-panel');
        const finalDuel = document.getElementById('final-duel');
        const toBank = document.getElementById('to-bank');
        const nextSum = document.getElementById('next-sum');
        const timerEl = document.getElementById('timer');
        const bankedEl = document.getElementById('banked');
        const bankZero = document.getElementById('bank-zero');
        const finalist1 = document.getElementById('finalist1');
        const finalist2 = document.getElementById('finalist2');
        const dots1 = document.getElementById('dots1');
        const dots2 = document.getElementById('dots2');

        function renderChain(bankChain, currentIndex) {
            if (!Array.isArray(bankChain) || bankChain.length === 0) { chainList.innerHTML = ''; return; }
            var arr = [0].concat(bankChain).slice().reverse();
            chainList.innerHTML = arr.map(function (val, i) {
                var idx = arr.length - 1 - i;
                var isActive = (currentIndex === idx);
                var disp = idx === 0 ? '0' : val.toLocaleString('ru-RU');
                var cls = 'chain-item' + (isActive ? ' active' : '');
                return ""<div class='"" + cls + ""'>"" + disp + ""</div>"";
            }).join('');
        }

        function renderDots(container, scoresStr) {
            var parts = (scoresStr || '').split(',').map(function (s) { return (s || '').trim(); });
            container.innerHTML = '';
            for (var i = 0; i < 5; i++) {
                var d = document.createElement('div');
                d.className = 'dot';
                var v = i < parts.length ? parts[i] : '-1';
                if (v === '1') d.classList.add('correct');
                else if (v === '0') d.classList.add('wrong');
                container.appendChild(d);
            }
        }

        function applyState(state) {
            if (!state) return;
            var isFinal = (state.state === 'FinalDuel');
            if (isFinal) {
                moneyTree.classList.add('hidden');
                bottomPanel.classList.add('hidden');
                finalDuel.classList.add('visible');
                header.textContent = 'ФИНАЛЬНАЯ ДУЭЛЬ';
                header.classList.add('final');
                finalist1.textContent = (state.finalist1 || 'ФИНАЛИСТ 1').toUpperCase();
                finalist2.textContent = (state.finalist2 || 'ФИНАЛИСТ 2').toUpperCase();
                renderDots(dots1, state.p1Scores);
                renderDots(dots2, state.p2Scores);
            } else {
                moneyTree.classList.remove('hidden');
                bottomPanel.classList.remove('hidden');
                finalDuel.classList.remove('visible');
                header.classList.remove('final');
                var nextS = state.nextSum != null ? Number(state.nextSum) : 1000;
                var qNum = state.questionNumber != null ? state.questionNumber : 0;
                header.textContent = (nextS.toLocaleString('ru-RU') + ' ВОПРОС : ' + (qNum || '—'));
                renderChain(state.bankChain || [], state.currentChainIndex != null ? state.currentChainIndex : 0);
            }
            questionText.textContent = state.question || 'ОЖИДАНИЕ ВОПРОСА ДЛЯ ВЕДУЩЕГО...';
            answerText.textContent = state.answer || 'ПРАВИЛЬНЫЙ ОТВЕТ';
            toBank.textContent = (state.toBank != null ? Number(state.toBank).toLocaleString('ru-RU') : 'XXXX');
            nextSum.textContent = (state.nextSum != null ? Number(state.nextSum).toLocaleString('ru-RU') : '2000');
            timerEl.textContent = state.timer || '0:00';
            bankedEl.textContent = (state.banked != null ? Number(state.banked).toLocaleString('ru-RU') : '0');
            bankZero.textContent = (state.banked != null ? Number(state.banked).toLocaleString('ru-RU') : '0');
        }

        function poll() {
            fetch('/api/host-state').then(function (r) { return r.json(); }).then(applyState).catch(function (e) { console.error('host-state', e); });
        }
        setInterval(poll, 800);
        poll();
    </script>
</body>
</html>";
        }
    }
}
