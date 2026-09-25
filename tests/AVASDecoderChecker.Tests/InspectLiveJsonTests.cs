using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AVASDecoderChecker.Tests
{
    public class InspectLiveJsonTests
    {
        private readonly ITestOutputHelper _output;

        public InspectLiveJsonTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void InspectDecoderStructure()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live_RX_DECODER_74fe488b22ca.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "golden_RX_DECODER_74fe488b22ca.json");
            }
            Assert.True(File.Exists(path), "File does not exist: " + path);

            string json = File.ReadAllText(path);
            var root = JObject.Parse(json);
            var dev = root["result"]?["devices"]?[0] as JObject;
            Assert.NotNull(dev);

            _output.WriteLine("=== DECODER: " + dev["device_id"] + " ===");

            // 1. Subscriptions
            _output.WriteLine("--- SUBSCRIPTIONS ---");
            var subs = dev["subscriptions"] as JArray;
            if (subs != null)
            {
                foreach (var s in subs)
                {
                    _output.WriteLine($"Sub type: {s["type"]} idx:{s["index"]} state:{s["status"]?["state"]} source_dev:{s["configuration"]?["source"]?["device_id"]} source_stream:{s["configuration"]?["source"]?["stream_index"]}");
                }
            }

            // 2. Streams
            _output.WriteLine("--- STREAMS ---");
            var streams = dev["streams"] as JArray;
            if (streams != null)
            {
                foreach (var st in streams)
                {
                    _output.WriteLine($"Stream type: {st["type"]} idx:{st["index"]} state:{st["status"]?["state"]} addr:{st["configuration"]?["address"]}");
                }
            }

            // 3. Nodes
            _output.WriteLine("--- NODES ---");
            var nodes = dev["nodes"] as JArray;
            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    string nType = n["type"]?.ToString() ?? "";
                    string nIdx = n["index"]?.ToString() ?? "";
                    string statusJson = n["status"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
                    string configJson = n["configuration"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
                    _output.WriteLine($"Node [{nType}] idx:{nIdx}\n   Status: {statusJson}\n   Config: {configJson}");
                }
            }
        }

        [Fact]
        public void InspectEncoderStructure()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live_TX_ENCODER_74fe488b07bb.json");
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            var root = JObject.Parse(json);
            var dev = root["result"]?["devices"]?[0] as JObject;
            Assert.NotNull(dev);

            _output.WriteLine("=== ENCODER: " + dev["device_id"] + " ===");

            // Streams
            _output.WriteLine("--- STREAMS ---");
            var streams = dev["streams"] as JArray;
            if (streams != null)
            {
                foreach (var st in streams)
                {
                    _output.WriteLine($"Stream type: {st["type"]} idx:{st["index"]} state:{st["status"]?["state"]} addr:{st["configuration"]?["address"]}");
                }
            }

            // Nodes
            _output.WriteLine("--- NODES ---");
            var nodes = dev["nodes"] as JArray;
            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    string nType = n["type"]?.ToString() ?? "";
                    string nIdx = n["index"]?.ToString() ?? "";
                    string statusJson = n["status"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
                    _output.WriteLine($"Node [{nType}] idx:{nIdx}\n   Status: {statusJson}");
                }
            }
        }
    }
}
