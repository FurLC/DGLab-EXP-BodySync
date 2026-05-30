using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DGLab.BepInEx.Protocol;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DGLab.BepInEx.Network
{
    internal sealed class DGLabEmbeddedWebSocketServer : IDGLabTransport
    {
        private readonly string _bindAddress;
        private readonly int _port;
        private readonly string _terminalId;
        private readonly object _sync = new object();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private TcpClient _appClient;
        private NetworkStream _appStream;
        private string _targetId = string.Empty;
        private bool _bound;

        public event Action<string> OnRawMessage;
        public event Action<DGLabMessage> OnMessage;
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public string ClientId => _terminalId;
        public string TargetId => _bound ? _targetId : string.Empty;
        public bool IsConnected => _listener != null;

        public DGLabEmbeddedWebSocketServer(string bindAddress, int port, string terminalId)
        {
            _bindAddress = string.IsNullOrWhiteSpace(bindAddress) ? "0.0.0.0" : bindAddress.Trim();
            _port = port <= 0 || port > 65535 ? 9999 : port;
            _terminalId = string.IsNullOrWhiteSpace(terminalId) ? Guid.NewGuid().ToString() : terminalId.Trim();
        }

        public void Connect()
        {
            Disconnect();

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(ParseBindAddress(_bindAddress), _port);
            _listener.Start();
            _ = Task.Run(() => AcceptLoop(_cts.Token));
            OnConnected?.Invoke();
        }

        public void Disconnect()
        {
            var hadListener = _listener != null;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _cts = null;
            CloseAppConnection(false);
            if (hadListener) OnClosed?.Invoke("embedded server stopped");
        }

        public void Send(object payload)
        {
            _ = SendMessageAsync(JsonConvert.SerializeObject(payload));
        }

        public void SendStrengthSet(int channel, int strength)
        {
            SendAppCommand("strength-" + channel + "+2+" + strength);
        }

        public void SendStrengthDelta(int channel, bool increase)
        {
            SendAppCommand("strength-" + channel + "+" + (increase ? "1" : "0") + "+1");
        }

        public void SendRawCommand(string command)
        {
            SendAppCommand(command);
        }

        public void SendWave(string channel, int timeSeconds, string wavePayload)
        {
            SendAppCommand("pulse-" + channel + ":[" + NormalizeWavePayload(channel, wavePayload) + "]");
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client, token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested) OnError?.Invoke(ex);
                }
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken token)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();
                var request = await ReadHttpRequest(stream, token);
                if (request.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    client.Close();
                    return;
                }

                var key = GetHeaderValue(request, "Sec-WebSocket-Key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    client.Close();
                    return;
                }

                await WriteHandshakeResponse(stream, key, token);
                SetAppConnection(client, stream);
                await ReceiveLoop(stream, token);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) OnError?.Invoke(ex);
            }
            finally
            {
                if (ReferenceEquals(stream, _appStream)) CloseAppConnection(true);
                else client.Close();
            }
        }

        private void SetAppConnection(TcpClient client, NetworkStream stream)
        {
            lock (_sync)
            {
                CloseAppConnection(false);
                _appClient = client;
                _appStream = stream;
                _targetId = string.Empty;
                _bound = false;
            }

            // Per official protocol: server must assign a targetId to the APP immediately after connection
            var appId = Guid.NewGuid().ToString("D");
            lock (_sync) { _targetId = appId; }
            Send(new DGLabMessage
            {
                type = "bind",
                clientId = appId,
                targetId = string.Empty,
                message = "targetId"
            });
        }

        private void CloseAppConnection(bool notify)
        {
            lock (_sync)
            {
                try { _appStream?.Close(); } catch { }
                try { _appClient?.Close(); } catch { }
                _appStream = null;
                _appClient = null;
                _targetId = string.Empty;
                _bound = false;
            }
            if (notify) OnClosed?.Invoke("app disconnected");
        }

        private void SendAppCommand(string command)
        {
            if (string.IsNullOrEmpty(TargetId)) return;
            Send(new DGLabMessage
            {
                type = "msg",
                clientId = ClientId,
                targetId = TargetId,
                message = command
            });
        }

        private async Task SendMessageAsync(string jsonPayload)
        {
            await _writeLock.WaitAsync();
            var needsCloseNotify = false;
            try
            {
                NetworkStream stream;
                TcpClient client;
                lock (_sync)
                {
                    stream = _appStream;
                    client = _appClient;
                }
                if (stream == null || client == null || !client.Connected) return;

                var payload = Encoding.UTF8.GetBytes(jsonPayload);
                var frame = BuildTextFrame(payload);
                await stream.WriteAsync(frame, 0, frame.Length);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                CloseAppConnection(false);
                needsCloseNotify = true;
            }
            finally
            {
                _writeLock.Release();
            }
            if (needsCloseNotify) OnClosed?.Invoke("app disconnected");
        }

        private async Task ReceiveLoop(NetworkStream stream, CancellationToken token)
        {
            var header = new byte[2];
            while (!token.IsCancellationRequested)
            {
                if (!await ReadExactlyAsync(stream, header, 2, token)) break;
                var opcode = header[0] & 0x0F;
                if (opcode == 0x8) break;

                var masked = (header[1] & 0x80) != 0;
                var length = header[1] & 0x7F;
                if (length == 126)
                {
                    var ext = new byte[2];
                    if (!await ReadExactlyAsync(stream, ext, 2, token)) break;
                    length = (ext[0] << 8) | ext[1];
                }
                else if (length == 127)
                {
                    var ext = new byte[8];
                    if (!await ReadExactlyAsync(stream, ext, 8, token)) break;
                    var longLength = BitConverter.ToUInt64(new[] { ext[7], ext[6], ext[5], ext[4], ext[3], ext[2], ext[1], ext[0] }, 0);
                    if (longLength > 65535) break;
                    length = (int)longLength;
                }

                var mask = new byte[4];
                if (masked && !await ReadExactlyAsync(stream, mask, 4, token)) break;

                var payload = new byte[length];
                if (length > 0 && !await ReadExactlyAsync(stream, payload, length, token)) break;
                if (masked)
                {
                    for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(payload[i] ^ mask[i % 4]);
                }

                if (opcode == 0x9)
                {
                    await SendControlFrame(stream, 0xA, payload, token);
                    continue;
                }
                if (opcode != 0x1) continue;

                HandleRawMessage(Encoding.UTF8.GetString(payload));
            }
        }

        private void HandleRawMessage(string payload)
        {
            OnRawMessage?.Invoke(payload);
            try
            {
                var msg = JsonConvert.DeserializeObject<DGLabMessage>(payload);
                if (msg == null) return;
                OnMessage?.Invoke(msg);

                if (IsPingMessage(msg))
                {
                    Send(new DGLabMessage
                    {
                        type = msg.type,
                        clientId = ClientId,
                        targetId = string.IsNullOrWhiteSpace(_targetId) ? msg.clientId : _targetId,
                        message = "pong"
                    });
                    return;
                }

                if (IsBindMessage(msg)) HandleBindMessage(msg);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private void HandleBindMessage(DGLabMessage msg)
        {
            // APP sends: clientId=terminal ID from QR, targetId=APP ID assigned by this server, message="DGLAB"
            string appId;
            lock (_sync) { appId = _targetId; }
            if (string.IsNullOrEmpty(appId)) return;

            if (!StringEquals(msg.clientId, ClientId)) return;
            if (!string.IsNullOrWhiteSpace(msg.targetId) && !StringEquals(msg.targetId, appId)) return;

            lock (_sync) { _targetId = appId; _bound = true; }

            Send(new DGLabMessage
            {
                type = "bind",
                clientId = ClientId,
                targetId = appId,
                message = "200"
            });
        }

        private bool IsBindMessage(DGLabMessage msg)
        {
            if (!StringEquals(msg.type, "bind")) return false;
            // Ignore our own "targetId" assignment message echoed back
            if (StringEquals(msg.message, "targetId")) return false;
            return true;
        }

        private static bool IsPingMessage(DGLabMessage msg)
        {
            return StringEquals(msg.type, "ping") ||
                   StringEquals(msg.type, "heartbeat") ||
                   StringEquals(msg.message, "ping") ||
                   StringEquals(msg.message, "heartbeat");
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> ReadHttpRequest(NetworkStream stream, CancellationToken token)
        {
            var buffer = new byte[4096];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, total, buffer.Length - total, token);
                if (read <= 0) break;
                total += read;
                var text = Encoding.UTF8.GetString(buffer, 0, total);
                if (text.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0) return text;
            }
            return Encoding.UTF8.GetString(buffer, 0, total);
        }

        private static string GetHeaderValue(string request, string header)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.StartsWith(header + ":", StringComparison.OrdinalIgnoreCase)) continue;
                return line.Substring(header.Length + 1).Trim();
            }
            return string.Empty;
        }

        private static async Task WriteHandshakeResponse(NetworkStream stream, string key, CancellationToken token)
        {
            var accept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(key.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                           "Upgrade: websocket\r\n" +
                           "Connection: Upgrade\r\n" +
                           "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
        }

        private static async Task<bool> ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
        {
            var total = 0;
            while (total < count)
            {
                var read = await stream.ReadAsync(buffer, total, count - total, token);
                if (read <= 0) return false;
                total += read;
            }
            return true;
        }

        private static byte[] BuildTextFrame(byte[] payload)
        {
            if (payload.Length < 126)
            {
                var frame = new byte[2 + payload.Length];
                frame[0] = 0x81;
                frame[1] = (byte)payload.Length;
                Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
                return frame;
            }
            var extended = new byte[4 + payload.Length];
            extended[0] = 0x81;
            extended[1] = 126;
            extended[2] = (byte)((payload.Length >> 8) & 0xFF);
            extended[3] = (byte)(payload.Length & 0xFF);
            Buffer.BlockCopy(payload, 0, extended, 4, payload.Length);
            return extended;
        }

        private static async Task SendControlFrame(NetworkStream stream, int opcode, byte[] payload, CancellationToken token)
        {
            if (payload == null) payload = new byte[0];
            if (payload.Length > 125) return;
            var frame = new byte[2 + payload.Length];
            frame[0] = (byte)(0x80 | opcode);
            frame[1] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
            await stream.WriteAsync(frame, 0, frame.Length, token);
        }

        private static IPAddress ParseBindAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "0.0.0.0") return IPAddress.Any;
            if (value == "127.0.0.1" || string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            if (IPAddress.TryParse(value, out var parsed)) return parsed;
            return IPAddress.Any;
        }

        private static string NormalizeWavePayload(string channel, string wavePayload)
        {
            if (string.IsNullOrEmpty(wavePayload)) return string.Empty;
            var prefix = channel + ":[";
            if (wavePayload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && wavePayload.EndsWith("]", StringComparison.Ordinal))
            {
                return wavePayload.Substring(prefix.Length, wavePayload.Length - prefix.Length - 1);
            }
            return wavePayload;
        }
    }
}
