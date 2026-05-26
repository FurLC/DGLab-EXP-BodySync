using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using BepInEx.Logging;

namespace DGLab.BepInEx
{
    internal sealed class DGLabQrService
    {
        private readonly ManualLogSource _log;
        private readonly Func<bool> _useEmbeddedServer;
        private readonly Func<string> _embeddedTerminalId;
        private readonly Func<string> _embeddedServerAddress;
        private readonly Func<int> _embeddedServerPort;
        private readonly Func<string> _serverUrl;
        private readonly Func<string> _qrWebSocketUrl;
        private readonly Func<string> _qrOutputDirectory;
        private string _lastQrUrl;
        private string _lastQrImagePath;
        private string _cachedEmbeddedAdvertiseAddress;
        private float _nextAddressCandidateLogTime;

        public DGLabQrService(
            ManualLogSource log,
            Func<bool> useEmbeddedServer,
            Func<string> embeddedTerminalId,
            Func<string> embeddedServerAddress,
            Func<int> embeddedServerPort,
            Func<string> serverUrl,
            Func<string> qrWebSocketUrl,
            Func<string> qrOutputDirectory)
        {
            _log = log;
            _useEmbeddedServer = useEmbeddedServer;
            _embeddedTerminalId = embeddedTerminalId;
            _embeddedServerAddress = embeddedServerAddress;
            _embeddedServerPort = embeddedServerPort;
            _serverUrl = serverUrl;
            _qrWebSocketUrl = qrWebSocketUrl;
            _qrOutputDirectory = qrOutputDirectory;
        }

        public string BuildQrUrl(string clientId)
        {
            return "https://www.dungeon-lab.com/app-download.php#DGLAB-SOCKET#" + BuildQrWebSocketUrl() + "/" + Uri.EscapeDataString(GetQrClientId(clientId));
        }

        public string BuildQrWebSocketUrl()
        {
            var configured = _qrWebSocketUrl != null ? _qrWebSocketUrl() : string.Empty;
            if (!string.IsNullOrWhiteSpace(configured)) return TrimTrailingSlash(configured.Trim());

            try
            {
                if (_useEmbeddedServer()) return "ws://" + GetEmbeddedAdvertiseAddress() + ":" + _embeddedServerPort();

                var url = _serverUrl();
                var uri = new Uri(url);
                if (IsLoopbackHost(uri.Host)) _log.LogWarning("DG-Lab QR backend URL uses loopback host. APP devices usually cannot reach 127.0.0.1 unless backend runs on the same device.");
                return TrimTrailingSlash(uri.ToString());
            }
            catch (Exception ex)
            {
                _log.LogWarning("DG-Lab failed to build QR WebSocket URL from ServerUrl. Falling back to raw value. " + ex.Message);
                return TrimTrailingSlash(_serverUrl());
            }
        }

        public string EnsureQrImage(string scanUrl)
        {
            if (!string.IsNullOrEmpty(_lastQrUrl) && _lastQrUrl == scanUrl && !string.IsNullOrEmpty(_lastQrImagePath) && File.Exists(_lastQrImagePath)) return _lastQrImagePath;

            try
            {
                _lastQrImagePath = DGLabQrImageWriter.WritePng(_qrOutputDirectory(), scanUrl);
                _lastQrUrl = scanUrl;
                _log.LogInfo("DG-Lab regenerated local QR image: " + _lastQrImagePath);
                return _lastQrImagePath;
            }
            catch (Exception ex)
            {
                _log.LogWarning("DG-Lab local QR image generation failed. " + ex.Message);
                return "<failed to generate local QR image>";
            }
        }

        public string GetQrClientId(string clientId)
        {
            if (_useEmbeddedServer()) return _embeddedTerminalId();
            return !string.IsNullOrWhiteSpace(clientId) ? clientId.Trim() : string.Empty;
        }

        public bool HasQrClientId(string clientId)
        {
            if (_useEmbeddedServer()) return !string.IsNullOrWhiteSpace(_embeddedTerminalId());
            return !string.IsNullOrWhiteSpace(clientId);
        }

        public void InvalidateAddressCache()
        {
            _cachedEmbeddedAdvertiseAddress = null;
            _cachedCandidates = null;
        }

        public void SetAdvertiseAddressOverride(string address)
        {
            _cachedEmbeddedAdvertiseAddress = address;
            _cachedCandidates = null;
        }

        public List<string> GetAdvertiseAddressList()
        {
            if (_cachedCandidates != null) return _cachedCandidates;
            var result = new List<string>();
            try
            {
                var candidates = GetAdvertiseAddressCandidates();
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                foreach (var c in candidates)
                    if (c.Score > -50) result.Add(c.Address.ToString());
            }
            catch { }
            _cachedCandidates = result;
            return result;
        }

        private List<string> _cachedCandidates;

        private string GetEmbeddedAdvertiseAddress()
        {
            var configuredAddress = _embeddedServerAddress();
            if (!string.IsNullOrWhiteSpace(configuredAddress)) return configuredAddress.Trim();
            if (!string.IsNullOrWhiteSpace(_cachedEmbeddedAdvertiseAddress)) return _cachedEmbeddedAdvertiseAddress;

            try
            {
                var candidates = GetAdvertiseAddressCandidates();
                if (candidates.Count > 0)
                {
                    candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
                    LogAddressCandidates(candidates);
                    _cachedEmbeddedAdvertiseAddress = candidates[0].Address.ToString();
                    return _cachedEmbeddedAdvertiseAddress;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("DG-Lab failed to detect LAN IP for QR. Set Network/EmbeddedServerAddress manually. " + ex.Message);
            }

            _cachedEmbeddedAdvertiseAddress = "127.0.0.1";
            return _cachedEmbeddedAdvertiseAddress;
        }

        private static List<AddressCandidate> GetAdvertiseAddressCandidates()
        {
            var candidates = new List<AddressCandidate>();
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface == null || networkInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                IPInterfaceProperties properties;
                try
                {
                    properties = networkInterface.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                var hasGateway = properties.GatewayAddresses != null && properties.GatewayAddresses.Count > 0;
                foreach (var unicast in properties.UnicastAddresses)
                {
                    var address = unicast != null ? unicast.Address : null;
                    if (address == null || address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;

                    candidates.Add(new AddressCandidate
                    {
                        Address = address,
                        InterfaceName = networkInterface.Name,
                        InterfaceDescription = networkInterface.Description,
                        Score = ScoreAddress(address, networkInterface, hasGateway)
                    });
                }
            }

            return candidates;
        }

        private static int ScoreAddress(IPAddress address, NetworkInterface networkInterface, bool hasGateway)
        {
            var bytes = address.GetAddressBytes();
            var score = 0;

            if (IsPrivateLanAddress(bytes)) score += 100;
            if (bytes[0] == 192 && bytes[1] == 168) score += 25;
            if (bytes[0] == 10) score += 18;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) score += 18;
            if (hasGateway) score += 30;
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 25;
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet || networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet) score += 20;
            if (bytes[0] == 169 && bytes[1] == 254) score -= 100;
            if (bytes[0] == 26) score -= 35;

            var name = ((networkInterface.Name ?? string.Empty) + " " + (networkInterface.Description ?? string.Empty)).ToLowerInvariant();
            if (name.Contains("virtual") || name.Contains("vmware") || name.Contains("virtualbox") || name.Contains("hyper-v") || name.Contains("loopback")) score -= 45;
            if (name.Contains("radmin") || name.Contains("zerotier") || name.Contains("hamachi") || name.Contains("tailscale")) score -= 25;

            return score;
        }

        private static bool IsPrivateLanAddress(byte[] bytes)
        {
            return bytes != null && bytes.Length == 4 && (bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168));
        }

        private void LogAddressCandidates(List<AddressCandidate> candidates)
        {
            if (_log == null || candidates == null || candidates.Count == 0) return;
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextAddressCandidateLogTime) return;
            _nextAddressCandidateLogTime = now + 10f;

            var message = new StringBuilder("DG-Lab QR address candidates:");
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                message.Append(" ").Append(candidate.Address).Append("(").Append(candidate.Score).Append(", ").Append(candidate.InterfaceName).Append(")");
            }

            _log.LogInfo(message.ToString());
        }

        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimTrailingSlash(string value)
        {
            return string.IsNullOrEmpty(value) ? value : value.TrimEnd(new[] { '/' });
        }

        private sealed class AddressCandidate
        {
            public IPAddress Address;
            public string InterfaceName;
            public string InterfaceDescription;
            public int Score;
        }
    }
}
