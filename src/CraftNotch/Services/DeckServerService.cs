using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CraftNotch.Models;

namespace CraftNotch.Services;

public class DeckServerService : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    public int Port { get; private set; } = 48823;
    public bool IsRunning => _listener?.IsListening ?? false;
    public string LocalIpAddress { get; private set; } = "127.0.0.1";
    public string ServerUrl => $"http://{LocalIpAddress}:{Port}/";

    // Callbacks to main application
    public Func<object>? GetStatusCallback { get; set; }
    public Action<int, int>? OnMouseMove { get; set; }
    public Action<string>? OnMouseClick { get; set; }
    public Action<string>? OnMediaCommand { get; set; }
    public Action<float>? OnVolumeChange { get; set; }
    public Action<string>? OnActionCommand { get; set; }
    public Action<string, string, string>? OnAgentWebhook { get; set; }

    public DeckServerService(int port = 47823)
    {
        Port = port;
        ResolveLocalIp();
    }

    private void ResolveLocalIp()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    LocalIpAddress = ip.ToString();
                    return;
                }
            }
        }
        catch
        {
            LocalIpAddress = "127.0.0.1";
        }
    }

    public void Start(int? preferredPort = null)
    {
        if (preferredPort.HasValue)
        {
            Port = preferredPort.Value;
        }

        Stop();
        _cts = new CancellationTokenSource();

        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{Port}/");
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            _listener = listener;
            Task.Run(() => ListenLoopAsync(listener, _cts.Token));
            System.Diagnostics.Debug.WriteLine($"DeckServer listening on port {Port}");

            // Optional: Try binding LAN IP in a separate listener so access-denied never blocks localhost
            if (LocalIpAddress != "127.0.0.1")
            {
                try
                {
                    var lanListener = new HttpListener();
                    lanListener.Prefixes.Add($"http://{LocalIpAddress}:{Port}/");
                    lanListener.Start();
                    Task.Run(() => ListenLoopAsync(lanListener, _cts.Token));
                }
                catch
                {
                    // Non-admin environments require urlacl reservation for external LAN IPs
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeckServer error starting on port {Port}: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
            _listener = null;
        }
        catch { }
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener != null && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeckServer loop exception: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // Enable CORS
        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

            if (path == "/" || path == "/deck" || path == "/index.html")
            {
                ServeDeckHtml(res);
            }
            else if (path == "/api/status")
            {
                ServeStatus(res);
            }
            else if (path == "/api/events")
            {
                ServeSseEvents(res);
            }
            else if (path == "/api/mouse")
            {
                HandleMouseMove(req, res);
            }
            else if (path == "/api/mouse/click")
            {
                HandleMouseClick(req, res);
            }
            else if (path == "/api/media")
            {
                HandleMediaCommand(req, res);
            }
            else if (path == "/api/volume")
            {
                HandleVolume(req, res);
            }
            else if (path == "/api/action")
            {
                HandleAction(req, res);
            }
            else if (path == "/api/agent")
            {
                HandleAgentWebhook(req, res);
            }
            else
            {
                res.StatusCode = 404;
                WriteResponse(res, "text/plain", "Not Found");
            }
        }
        catch (Exception ex)
        {
            try
            {
                res.StatusCode = 500;
                WriteResponse(res, "text/plain", $"Server Error: {ex.Message}");
            }
            catch { }
        }
    }

    private void ServeStatus(HttpListenerResponse res)
    {
        var data = GetStatusCallback != null ? GetStatusCallback() : new { status = "ok" };
        var json = JsonSerializer.Serialize(data);
        res.StatusCode = 200;
        WriteResponse(res, "application/json", json);
    }

    private void ServeSseEvents(HttpListenerResponse res)
    {
        res.ContentType = "text/event-stream";
        res.Headers.Add("Cache-Control", "no-cache");
        res.Headers.Add("Connection", "keep-alive");

        try
        {
            using var writer = new StreamWriter(res.OutputStream);
            for (int i = 0; i < 60; i++)
            {
                if (_cts == null || _cts.IsCancellationRequested) break;
                var data = GetStatusCallback != null ? GetStatusCallback() : new { status = "ok" };
                var json = JsonSerializer.Serialize(data);
                writer.WriteLine($"data: {json}\n");
                writer.Flush();
                Thread.Sleep(1000);
            }
        }
        catch { }
        finally
        {
            try { res.Close(); } catch { }
        }
    }

    private void HandleMouseMove(HttpListenerRequest req, HttpListenerResponse res)
    {
        int dx = 0, dy = 0;
        if (int.TryParse(req.QueryString["dx"], out var x)) dx = x;
        if (int.TryParse(req.QueryString["dy"], out var y)) dy = y;

        OnMouseMove?.Invoke(dx, dy);

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"ok\"}");
    }

    private void HandleMouseClick(HttpListenerRequest req, HttpListenerResponse res)
    {
        var button = req.QueryString["button"] ?? "left";
        OnMouseClick?.Invoke(button);

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"ok\"}");
    }

    private void HandleMediaCommand(HttpListenerRequest req, HttpListenerResponse res)
    {
        var cmd = req.QueryString["cmd"] ?? "playpause";
        OnMediaCommand?.Invoke(cmd);

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"ok\"}");
    }

    private void HandleVolume(HttpListenerRequest req, HttpListenerResponse res)
    {
        if (float.TryParse(req.QueryString["level"], out var level))
        {
            OnVolumeChange?.Invoke(level);
        }

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"ok\"}");
    }

    private void HandleAction(HttpListenerRequest req, HttpListenerResponse res)
    {
        var cmd = req.QueryString["cmd"] ?? "";
        OnActionCommand?.Invoke(cmd);

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"ok\"}");
    }

    private void HandleAgentWebhook(HttpListenerRequest req, HttpListenerResponse res)
    {
        if (req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            var body = reader.ReadToEnd();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                string agentName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Agent" : "Agent";
                string action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "Working" : "Working";

                OnAgentWebhook?.Invoke(agentName, action, status);
            }
            catch { }
        }

        res.StatusCode = 200;
        WriteResponse(res, "application/json", "{\"status\":\"accepted\"}");
    }

    private void ServeDeckHtml(HttpListenerResponse res)
    {
        res.StatusCode = 200;
        WriteResponse(res, "text/html; charset=utf-8", DeckHtmlContent);
    }

    private void WriteResponse(HttpListenerResponse res, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        res.ContentType = contentType;
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }

    // Modern responsive mobile web companion
    private const string DeckHtmlContent = @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
  <title>Notch Mobile Companion</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; -webkit-user-select: none; }
    body {
      background: #090A0F;
      color: #F3F4F6;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      padding: 16px;
      touch-action: pan-y;
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      background: rgba(255, 255, 255, 0.05);
      border-radius: 20px;
      backdrop-filter: blur(20px);
      border: 1px solid rgba(255, 255, 255, 0.1);
    }
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      font-weight: 600;
    }
    .badge-mint { color: #34D399; }
    .badge-blue { color: #60A5FA; }
    .badge-purple { color: #A78BFA; }

    .card {
      background: rgba(18, 20, 29, 0.85);
      border-radius: 24px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      padding: 18px;
      backdrop-filter: blur(20px);
      box-shadow: 0 10px 30px rgba(0,0,0,0.5);
    }

    .trackpad-zone {
      flex: 1;
      min-height: 220px;
      background: rgba(255, 255, 255, 0.02);
      border: 2px dashed rgba(255, 255, 255, 0.15);
      border-radius: 24px;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      touch-action: none;
      position: relative;
    }
    .trackpad-zone:active {
      background: rgba(52, 211, 153, 0.04);
      border-color: #34D399;
    }
    .trackpad-label {
      color: #6B7280;
      font-size: 14px;
      font-weight: 500;
      pointer-events: none;
    }

    .media-info {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      margin-bottom: 12px;
    }
    .media-title {
      font-size: 16px;
      font-weight: 700;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 90%;
    }
    .media-artist {
      font-size: 13px;
      color: #9CA3AF;
      margin-top: 2px;
    }

    .controls-row {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 20px;
      margin-top: 8px;
    }
    .btn-circle {
      width: 52px;
      height: 52px;
      border-radius: 26px;
      background: rgba(255, 255, 255, 0.1);
      border: none;
      color: white;
      font-size: 20px;
      display: flex;
      justify-content: center;
      align-items: center;
      cursor: pointer;
      transition: transform 0.1s;
    }
    .btn-circle:active {
      transform: scale(0.92);
      background: rgba(255, 255, 255, 0.2);
    }
    .btn-play {
      width: 64px;
      height: 64px;
      border-radius: 32px;
      background: #34D399;
      color: #090A0F;
    }

    .actions-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 10px;
    }
    .btn-action {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 12px 6px;
      color: #D1D5DB;
      font-size: 12px;
      font-weight: 600;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      cursor: pointer;
    }
    .btn-action:active {
      background: rgba(255, 255, 255, 0.15);
    }

    .slider-container {
      margin-top: 14px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    input[type=range] {
      flex: 1;
      height: 8px;
      border-radius: 4px;
      accent-color: #34D399;
      background: rgba(255, 255, 255, 0.15);
    }
  </style>
</head>
<body>
  <div class=""header"">
    <div class=""badge badge-mint"" id=""cpuBadge"">⚡ CPU: --%</div>
    <div class=""badge badge-blue"" id=""ramBadge"">💾 RAM: --%</div>
    <div class=""badge badge-purple"" id=""batBadge"">🔋 --%</div>
  </div>

  <div class=""trackpad-zone"" id=""trackpad"">
    <div class=""trackpad-label"">👆 Smooth Trackpad</div>
    <div class=""trackpad-label"" style=""font-size: 11px; margin-top: 4px;"">Tap: Left Click | 2-Finger: Right Click</div>
  </div>

  <div class=""card"">
    <div class=""media-info"">
      <div class=""media-title"" id=""mediaTitle"">No Media Playing</div>
      <div class=""media-artist"" id=""mediaArtist"">Windows System Audio</div>
    </div>
    <div class=""controls-row"">
      <button class=""btn-circle"" onclick=""sendMedia('prev')"">⏮</button>
      <button class=""btn-circle btn-play"" id=""playBtn"" onclick=""sendMedia('playpause')"">▶</button>
      <button class=""btn-circle"" onclick=""sendMedia('next')"">⏭</button>
    </div>
    <div class=""slider-container"">
      <span>🔈</span>
      <input type=""range"" id=""volumeSlider"" min=""0"" max=""100"" value=""50"" oninput=""sendVolume(this.value)"">
      <span>🔊</span>
    </div>
  </div>

  <div class=""card actions-grid"">
    <button class=""btn-action"" onclick=""sendAction('mute')"">
      <span style=""font-size: 18px;"">🔇</span>
      <span>Mute</span>
    </button>
    <button class=""btn-action"" onclick=""sendAction('snip')"">
      <span style=""font-size: 18px;"">📸</span>
      <span>Snip</span>
    </button>
    <button class=""btn-action"" onclick=""sendAction('lock')"">
      <span style=""font-size: 18px;"">🔒</span>
      <span>Lock</span>
    </button>
    <button class=""btn-action"" onclick=""sendAction('expand')"">
      <span style=""font-size: 18px;"">🏝️</span>
      <span>Notch</span>
    </button>
  </div>

  <script>
    let lastX = 0, lastY = 0;
    let touchStartTime = 0;
    let touchCount = 0;
    const trackpad = document.getElementById('trackpad');

    trackpad.addEventListener('touchstart', (e) => {
      touchCount = e.touches.length;
      touchStartTime = Date.now();
      lastX = e.touches[0].clientX;
      lastY = e.touches[0].clientY;
    });

    trackpad.addEventListener('touchmove', (e) => {
      e.preventDefault();
      const currentX = e.touches[0].clientX;
      const currentY = e.touches[0].clientY;
      const dx = Math.round((currentX - lastX) * 1.5);
      const dy = Math.round((currentY - lastY) * 1.5);
      lastX = currentX;
      lastY = currentY;

      if (Math.abs(dx) > 0 || Math.abs(dy) > 0) {
        fetch(`/api/mouse?dx=${dx}&dy=${dy}`, { method: 'POST' }).catch(()=>{});
      }
    });

    trackpad.addEventListener('touchend', (e) => {
      const duration = Date.now() - touchStartTime;
      if (duration < 250) {
        if (touchCount >= 2) {
          fetch('/api/mouse/click?button=right', { method: 'POST' }).catch(()=>{});
        } else {
          fetch('/api/mouse/click?button=left', { method: 'POST' }).catch(()=>{});
        }
      }
    });

    function sendMedia(cmd) {
      fetch(`/api/media?cmd=${cmd}`, { method: 'POST' }).catch(()=>{});
    }

    function sendVolume(val) {
      fetch(`/api/volume?level=${val}`, { method: 'POST' }).catch(()=>{});
    }

    function sendAction(cmd) {
      fetch(`/api/action?cmd=${cmd}`, { method: 'POST' }).catch(()=>{});
    }

    // Live update status via SSE or polling
    function updateStatus() {
      fetch('/api/status')
        .then(r => r.json())
        .then(data => {
          if (data.cpu !== undefined) document.getElementById('cpuBadge').innerText = `⚡ CPU: ${data.cpu}%`;
          if (data.ram !== undefined) document.getElementById('ramBadge').innerText = `💾 RAM: ${data.ram}%`;
          if (data.battery !== undefined) document.getElementById('batBadge').innerText = `🔋 ${data.battery}%`;
          if (data.media) {
            document.getElementById('mediaTitle').innerText = data.media.title || 'No Media Playing';
            document.getElementById('mediaArtist').innerText = data.media.artist || 'Windows System Audio';
            document.getElementById('playBtn').innerText = data.media.isPlaying ? '⏸' : '▶';
          }
          if (data.volume !== undefined && document.activeElement !== document.getElementById('volumeSlider')) {
            document.getElementById('volumeSlider').value = data.volume;
          }
        })
        .catch(()=>{});
    }

    setInterval(updateStatus, 1500);
    updateStatus();
  </script>
</body>
</html>";
}
