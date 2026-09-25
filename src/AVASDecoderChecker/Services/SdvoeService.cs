using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VOIPS_LIB;

namespace AVASDecoderChecker.Services
{
    public class SdvoeService : ISdvoeService
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private readonly object _sdkLock = new();
        private VOIPS? _voips;
        private string? _currentIp;
        private int _currentPort = -1;

        private VOIPS GetOrInitSdk(string ip, int port)
        {
            lock (_sdkLock)
            {
                if (_voips == null || _currentIp != ip || _currentPort != port)
                {
                    _voips = new VOIPS();
                    _voips.SET_CONTROL_SERVER(ip, port);
                    _currentIp = ip;
                    _currentPort = port;
                }
                return _voips;
            }
        }

        public static async Task<string> ResolveEffectiveHostAsync(string hostOrIp)
        {
            if (string.IsNullOrWhiteSpace(hostOrIp)) return "127.0.0.1";
            string trimmed = hostOrIp.Trim();
            if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return "127.0.0.1";
            }

            if (System.Net.IPAddress.TryParse(trimmed, out _))
            {
                return trimmed;
            }

            try
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(trimmed);
                var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 != null)
                {
                    return ipv4.ToString();
                }
            }
            catch
            {
                // Fallback to original string
            }

            return trimmed;
        }

        public async Task<(bool Success, string Message, long LatencyMs, int DetectedPort)> TestConnectionAsync(string ip, int port, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            int targetPort = port;
            string effectiveIp = await ResolveEffectiveHostAsync(ip);

            try
            {
                // Try configured port first
                bool isPortOpen = await CheckTcpPortAsync(effectiveIp, targetPort, Math.Min(1500, timeoutMs));

                // If specified port is closed and is standard 80 or 8090, probe alternative port
                if (!isPortOpen)
                {
                    int altPort = (targetPort == 80) ? 8090 : (targetPort == 8090 ? 80 : 0);
                    if (altPort > 0 && await CheckTcpPortAsync(effectiveIp, altPort, 1000))
                    {
                        targetPort = altPort;
                        isPortOpen = true;
                    }
                }

                if (!isPortOpen)
                {
                    sw.Stop();
                    return (false, $"Could not connect to {ip}:{port} (resolved: {effectiveIp}:{targetPort}). Verify that the SDVoE Control Server is running and reachable on this port.", sw.ElapsedMilliseconds, port);
                }

                // SDVoE SDK verification via DiscoveryIdentity
                var voips = GetOrInitSdk(effectiveIp, targetPort);
                var discoveryResult = await Task.Run(() =>
                {
                    lock (_sdkLock)
                    {
                        var res = voips.DiscoveryIdentity();
                        if (res == null || res.status != "SUCCESS")
                        {
                            res = voips.Discovery();
                        }
                        return res;
                    }
                });

                sw.Stop();

                if (discoveryResult?.status == "SUCCESS" && discoveryResult.result?.devices != null)
                {
                    int totalDevices = discoveryResult.result.devices.Count;
                    int rxCount = discoveryResult.result.devices.Count(d => d.identity?.is_receiver == true);
                    string portNotice = (targetPort != port) ? $" (Auto-detected active port: {targetPort})" : "";

                    return (
                        true,
                        $"Connected successfully to SDVoE Control Server! Found {rxCount} decoder(s) ({totalDevices} total devices){portNotice}.",
                        sw.ElapsedMilliseconds,
                        targetPort
                    );
                }

                return (
                    true,
                    $"Connected to server at {ip}:{targetPort}, but discovery returned no devices. (Verify network cables & SDVoE switches).",
                    sw.ElapsedMilliseconds,
                    targetPort
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, $"Connection error: {ex.Message}", sw.ElapsedMilliseconds, port);
            }
        }

        private readonly ConcurrentDictionary<string, string> _multicastToEncoderMap = new(StringComparer.OrdinalIgnoreCase);

        public async Task<List<DecoderItem>> QueryDecodersAsync(string ip, int port, int timeoutMs = 5000)
        {
            var decoders = new List<DecoderItem>();
            string effectiveIp = await ResolveEffectiveHostAsync(ip);
            var voips = GetOrInitSdk(effectiveIp, port);

            return await Task.Run(async () =>
            {
                VOIPS.BR_RESULT? discoveryResult;
                lock (_sdkLock)
                {
                    discoveryResult = voips.DiscoveryIdentity();
                    if (discoveryResult == null || discoveryResult.status != "SUCCESS" || discoveryResult.result?.devices?.Count == 0)
                    {
                        discoveryResult = voips.Discovery();
                    }
                }

                if (discoveryResult?.status == "SUCCESS" && discoveryResult.result?.devices != null)
                {
                    foreach (var dev in discoveryResult.result.devices)
                    {
                        bool isTx = dev.identity?.is_transmitter ?? (dev.identity?.is_receiver == false);
                        string mac = dev.device_id ?? string.Empty;

                        // If transmitter, discover published multicast streams
                        if (isTx && !string.IsNullOrWhiteSpace(mac))
                        {
                            if (dev.streams != null)
                            {
                                foreach (var st in dev.streams)
                                {
                                    string addr = st.configuration?.address ?? "";
                                    if (!string.IsNullOrWhiteSpace(addr) && addr != "0.0.0.0")
                                    {
                                        _multicastToEncoderMap[addr] = mac;
                                    }
                                }
                            }
                        }

                        // Check if device is a receiver (decoder)
                        bool isRx = dev.identity?.is_receiver ?? (dev.identity?.is_transmitter == false);
                        if (!isRx) continue;

                        if (string.IsNullOrWhiteSpace(mac)) continue;

                        string name = !string.IsNullOrWhiteSpace(dev.configuration?.device_name)
                            ? dev.configuration.device_name
                            : mac;

                        // Query MCU IP address with timeout
                        string devIp = effectiveIp;
                        try
                        {
                            var ipTask = voips.GET_MCU_IP(mac);
                            if (ipTask.Wait(800) && !string.IsNullOrWhiteSpace(ipTask.Result))
                            {
                                devIp = ipTask.Result;
                            }
                        }
                        catch
                        {
                            // Fallback to server IP
                        }

                        decoders.Add(new DecoderItem
                        {
                            MacAddress = mac,
                            DeviceName = name,
                            IpAddress = devIp,
                            IsReceiver = true,
                            IsOnline = dev.status?.active ?? true,
                            IsSelected = true
                        });
                    }
                }

                // If still empty, attempt direct HTTP REST query fallback
                if (decoders.Count == 0)
                {
                    try
                    {
                        string url = $"http://{effectiveIp}:{port}/api/devices";
                        var response = await HttpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            var parsed = JToken.Parse(json);
                            JArray? devArray = parsed as JArray ?? parsed["result"]?["devices"] as JArray;
                            if (devArray != null)
                            {
                                foreach (var d in devArray)
                                {
                                    bool isRx = d["identity"]?["is_receiver"]?.Value<bool>() ?? (d["identity"]?["is_transmitter"]?.Value<bool>() == false);
                                    if (isRx)
                                    {
                                        string devMac = d["device_id"]?.ToString() ?? "";
                                        string devName = d["configuration"]?["device_name"]?.ToString() ?? devMac;
                                        bool active = d["status"]?["active"]?.Value<bool>() ?? true;

                                        decoders.Add(new DecoderItem
                                        {
                                            MacAddress = devMac,
                                            DeviceName = devName,
                                            IpAddress = effectiveIp,
                                            IsReceiver = true,
                                            IsOnline = active,
                                            IsSelected = true
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore fallback failure
                    }
                }

                return decoders;
            });
        }

        public async Task<DecoderTelemetrySample> SampleDecoderTelemetryAsync(string ip, int port, DecoderItem decoder, int timeoutMs = 3000)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            string effectiveIp = await ResolveEffectiveHostAsync(ip);
            var voips = GetOrInitSdk(effectiveIp, port);

            return await Task.Run(async () =>
            {
                var encoderMapDict = new Dictionary<string, string>(_multicastToEncoderMap);

                try
                {
                    var devObj = await FetchDeviceObjectAsync(voips, decoder.MacAddress, timeoutMs);
                    if (devObj != null)
                    {
                        var sample = HealthEvaluator.EvaluateDevice(devObj, decoder.IpAddress, encoderMapDict);
                        sample.DecoderName = !string.IsNullOrWhiteSpace(decoder.DeviceName) ? decoder.DeviceName : sample.DecoderName;
                        sample.MacAddress = decoder.MacAddress;
                        return sample;
                    }

                    // Fallback: direct REST query if available
                    try
                    {
                        string url = $"http://{ip}:{port}/api/devices/{decoder.MacAddress}";
                        var response = await HttpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            return HealthEvaluator.EvaluateJson(json, decoder.MacAddress, decoder.IpAddress, encoderMapDict);
                        }
                    }
                    catch
                    {
                        // Ignore fallback failure
                    }

                    return new DecoderTelemetrySample
                    {
                        Timestamp = DateTime.Now,
                        DecoderName = decoder.DeviceName,
                        MacAddress = decoder.MacAddress,
                        IpAddress = decoder.IpAddress,
                        OverallStatus = "FAIL",
                        FaultAttribution = "SERVER_TIMEOUT",
                        Notes = "No response from SDVoE server for this device"
                    };
                }
                catch (Exception ex)
                {
                    return new DecoderTelemetrySample
                    {
                        Timestamp = DateTime.Now,
                        DecoderName = decoder.DeviceName,
                        MacAddress = decoder.MacAddress,
                        IpAddress = decoder.IpAddress,
                        OverallStatus = "FAIL",
                        FaultAttribution = "SERVER_TIMEOUT",
                        Notes = $"Query exception: {ex.Message}"
                    };
                }
            });
        }

        public async Task<List<EncoderItem>> QueryEncodersAsync(string ip, int port, int timeoutMs = 5000)
        {
            string effectiveIp = await ResolveEffectiveHostAsync(ip);
            var voips = GetOrInitSdk(effectiveIp, port);

            return await Task.Run(() =>
            {
                var encoders = new List<EncoderItem>();

                VOIPS.BR_RESULT? disc = null;
                lock (_sdkLock)
                {
                    try
                    {
                        disc = voips.DiscoveryIdentity();
                    }
                    catch
                    {
                        disc = null;
                    }
                }

                if (disc?.result?.devices != null)
                {
                    foreach (var dev in disc.result.devices)
                    {
                        string mac = dev.device_id ?? "";
                        bool isTx = dev.identity?.is_transmitter ?? (dev.identity?.is_receiver == false);
                        if (!isTx || string.IsNullOrWhiteSpace(mac)) continue;

                        string name = !string.IsNullOrWhiteSpace(dev.configuration?.device_name)
                            ? dev.configuration.device_name
                            : mac;

                        string devIp = effectiveIp;
                        try
                        {
                            var ipTask = voips.GET_MCU_IP(mac);
                            if (ipTask.Wait(800) && !string.IsNullOrWhiteSpace(ipTask.Result))
                            {
                                devIp = ipTask.Result;
                            }
                        }
                        catch
                        {
                            // fallback
                        }

                        string streamAddr = "";
                        if (dev.streams != null)
                        {
                            foreach (var st in dev.streams)
                            {
                                string addr = st.configuration?.address ?? "";
                                if (!string.IsNullOrWhiteSpace(addr) && addr != "0.0.0.0")
                                {
                                    streamAddr = addr;
                                    _multicastToEncoderMap[addr] = mac;
                                    break;
                                }
                            }
                        }

                        encoders.Add(new EncoderItem
                        {
                            MacAddress = mac,
                            DeviceName = name,
                            IpAddress = devIp,
                            IsTransmitter = true,
                            IsOnline = dev.status?.active ?? true,
                            MulticastAddress = streamAddr
                        });
                    }
                }

                return encoders;
            });
        }

        public async Task<EncoderTelemetrySample> SampleEncoderTelemetryAsync(string ip, int port, EncoderItem encoder, int timeoutMs = 3000)
        {
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));
            string effectiveIp = await ResolveEffectiveHostAsync(ip);
            var voips = GetOrInitSdk(effectiveIp, port);

            return await Task.Run(async () =>
            {
                try
                {
                    var devObj = await FetchDeviceObjectAsync(voips, encoder.MacAddress, timeoutMs);
                    if (devObj != null)
                    {
                        string json = JsonConvert.SerializeObject(new { status = "SUCCESS", result = new { devices = new[] { devObj } } });
                        var sample = HealthEvaluator.EvaluateEncoder(json, encoder);
                        sample.EncoderName = !string.IsNullOrWhiteSpace(encoder.DeviceName) ? encoder.DeviceName : sample.EncoderName;
                        sample.MacAddress = encoder.MacAddress;
                        return sample;
                    }

                    // Fallback: direct REST query
                    try
                    {
                        string url = $"http://{effectiveIp}:{port}/api/devices/{encoder.MacAddress}";
                        var response = await HttpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            return HealthEvaluator.EvaluateEncoder(json, encoder);
                        }
                    }
                    catch
                    {
                        // Ignore REST fallback failure
                    }
                }
                catch
                {
                    // Fall through
                }

                return new EncoderTelemetrySample
                {
                    EncoderName = encoder.DeviceName,
                    MacAddress = encoder.MacAddress,
                    IpAddress = encoder.IpAddress,
                    OverallStatus = "FAIL",
                    Notes = "Encoder query timed out or unreachable"
                };
            });
        }

        private async Task<VOIPS.DEVICE_OBJECT?> FetchDeviceObjectAsync(VOIPS voips, string mac, int timeoutMs)
        {
            VOIPS.BR_RESULT initialResult;
            lock (_sdkLock)
            {
                initialResult = voips.GET_ALL_SETTINGS(mac);
            }

            if (initialResult == null) return null;

            // Direct SUCCESS with device data
            if (initialResult.result?.devices != null && initialResult.result.devices.Count > 0)
            {
                return initialResult.result.devices.FirstOrDefault(d => string.Equals(d.device_id, mac, StringComparison.OrdinalIgnoreCase))
                       ?? initialResult.result.devices.First();
            }

            // Asynchronous PROCESSING workflow with request_id
            if (initialResult.status == "PROCESSING" && initialResult.request_id != null)
            {
                int reqId = (int)initialResult.request_id;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                while (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(150);

                    VOIPS.BR_RESULT reqResult;
                    lock (_sdkLock)
                    {
                        reqResult = voips.GET_REQUEST(reqId);
                    }

                    if (reqResult?.result?.devices != null && reqResult.result.devices.Count > 0)
                    {
                        return reqResult.result.devices.FirstOrDefault(d => string.Equals(d.device_id, mac, StringComparison.OrdinalIgnoreCase))
                               ?? reqResult.result.devices.First();
                    }
                }
            }

            return null;
        }

        private static async Task<bool> CheckTcpPortAsync(string ip, int port, int timeoutMs)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(ip, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask)
                {
                    return tcpClient.Connected;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
