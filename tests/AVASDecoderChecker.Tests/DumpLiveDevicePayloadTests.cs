using System;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Services;
using Newtonsoft.Json;
using VOIPS_LIB;
using Xunit;
using Xunit.Abstractions;

namespace AVASDecoderChecker.Tests
{
    public class DumpLiveDevicePayloadTests
    {
        private readonly ITestOutputHelper _output;

        public DumpLiveDevicePayloadTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DumpLiveDecoderAndEncoderSettings()
        {
            var voips = new VOIPS();
            voips.SET_CONTROL_SERVER("127.0.0.1", 8090);

            var disc = voips.DiscoveryIdentity();
            if (disc?.result?.devices == null || disc.result.devices.Count == 0)
            {
                _output.WriteLine("No devices found or control server offline.");
                return;
            }

            foreach (var dev in disc.result.devices)
            {
                string mac = dev.device_id;
                bool isRx = dev.identity?.is_receiver ?? false;
                string role = isRx ? "RX_DECODER" : "TX_ENCODER";
                _output.WriteLine($"Querying {role} - MAC: {mac}...");

                var settingsRes = voips.GET_ALL_SETTINGS(mac);
                if (settingsRes?.status == "PROCESSING" && settingsRes.request_id != null)
                {
                    int reqId = (int)settingsRes.request_id;
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(200);
                        var req = voips.GET_REQUEST(reqId);
                        if (req?.result?.devices != null && req.result.devices.Count > 0)
                        {
                            settingsRes = req;
                            break;
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(settingsRes, Formatting.Indented);
                string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"live_{role}_{mac}.json");
                await File.WriteAllTextAsync(outPath, json);
                _output.WriteLine($"Saved {role} {mac} to {outPath} ({json.Length} chars)");

                // Print first 500 chars snippet
                _output.WriteLine(json.Length > 800 ? json.Substring(0, 800) + "..." : json);
            }
        }
    }
}
