using System;
using System.Collections.Generic;
using System.Linq;
using AVASDecoderChecker.Models;
using Newtonsoft.Json.Linq;
using VOIPS_LIB;

namespace AVASDecoderChecker.Services
{
    public static class HealthEvaluator
    {
        public static DecoderTelemetrySample Evaluate(
            bool isStreaming,
            bool isDisplayConnected,
            bool isStable,
            string subscriptionState,
            string resolutionTiming,
            string sourceTxMac = "N/A",
            string decoderName = "",
            string macAddress = "",
            string ipAddress = "")
        {
            var sample = new DecoderTelemetrySample
            {
                Timestamp = DateTime.Now,
                DecoderName = string.IsNullOrWhiteSpace(decoderName) ? (string.IsNullOrWhiteSpace(macAddress) ? "Unknown" : macAddress) : decoderName,
                MacAddress = macAddress,
                IpAddress = ipAddress,
                IsHpdConnected = isDisplayConnected,
                IsClockStable = isStable,
                SourceEncoderMac = string.IsNullOrWhiteSpace(sourceTxMac) ? "N/A" : sourceTxMac,
                VideoRaster = string.IsNullOrWhiteSpace(resolutionTiming) ? "No Signal" : resolutionTiming
            };

            string subState = string.IsNullOrWhiteSpace(subscriptionState) ? "UNSUBSCRIBED" : subscriptionState.ToUpperInvariant();
            bool isRouteSubscribed = subState == "CONNECTED" || subState == "SUBSCRIBED" || subState == "ACTIVE" || subState == "STREAMING";

            // Question 1 logic
            if (isStreaming && isStable && isRouteSubscribed)
            {
                sample.VideoReceivedStatus = "RECEIVED";
                sample.Question1Badge = "VIDEO RECEIVED";
                sample.Question1Details = $"Video stream locked ({sample.VideoRaster})";
            }
            else if (isRouteSubscribed)
            {
                sample.VideoReceivedStatus = "NO_STREAM";
                sample.Question1Badge = "NO VIDEO STREAM";
                sample.Question1Details = isStreaming ? "Clock sync unstable" : "Stream inactive";
            }
            else
            {
                sample.VideoReceivedStatus = "UNSUBSCRIBED";
                sample.Question1Badge = "UNSUBSCRIBED";
                sample.Question1Details = $"Subscription: {subState}";
            }

            // Question 2 logic
            if (!isDisplayConnected)
            {
                sample.DisplayScreenStatus = "NO_DISPLAY";
                sample.DisplayModelName = "No Display Detected";
                sample.Question2Badge = "NO CABLE / DISPLAY OFF";
                sample.Question2Details = "Display disconnected";
            }
            else if (sample.VideoReceivedStatus == "RECEIVED")
            {
                sample.DisplayScreenStatus = "DISPLAYING_VIDEO";
                sample.DisplayModelName = "Connected Display";
                sample.IsTmdsClockActive = true;
                sample.Question2Badge = "DISPLAYING VIDEO";
                sample.Question2Details = "Displaying active video";
            }
            else
            {
                sample.DisplayScreenStatus = "WAITING_FOR_SOURCE";
                sample.DisplayModelName = "Connected Display";
                sample.Question2Badge = "WAITING FOR SOURCE";
                sample.Question2Details = "Waiting for active video";
            }

            var notes = new List<string>();
            if (!isDisplayConnected) notes.Add("Display Disconnected (HPD Low)");
            if (!isStreaming) notes.Add("Stream Inactive");
            if (!isRouteSubscribed) notes.Add($"Subscription: {subState}");
            if (!isStable && isStreaming) notes.Add("Clock Sync Unstable");

            if (!isDisplayConnected || !isStreaming)
            {
                sample.OverallStatus = "FAIL";
            }
            else if (!isRouteSubscribed || !isStable)
            {
                sample.OverallStatus = "WARN";
            }
            else
            {
                sample.OverallStatus = "PASS";
            }

            sample.Notes = notes.Count > 0 ? string.Join("; ", notes) : "Healthy - Active Stream";
            return sample;
        }

        public static DecoderTelemetrySample EvaluateJson(string json, string fallbackMac = "", string fallbackIp = "", Dictionary<string, string>? multicastToEncoderMap = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new DecoderTelemetrySample
                {
                    OverallStatus = "FAIL",
                    FaultAttribution = "SERVER_TIMEOUT",
                    Notes = "Empty JSON payload"
                };
            }

            try
            {
                var token = JToken.Parse(json);
                JObject? dev = token as JObject;

                if (dev?["result"]?["devices"] is JArray devArray && devArray.Count > 0)
                {
                    dev = devArray[0] as JObject;
                }
                else if (dev?["devices"] is JArray devArr && devArr.Count > 0)
                {
                    dev = devArr[0] as JObject;
                }

                if (dev == null)
                {
                    return new DecoderTelemetrySample
                    {
                        OverallStatus = "FAIL",
                        FaultAttribution = "SERVER_TIMEOUT",
                        Notes = "No device found in JSON"
                    };
                }

                return EvaluateParsedJObject(dev, fallbackMac, fallbackIp, multicastToEncoderMap);
            }
            catch (Exception ex)
            {
                return new DecoderTelemetrySample
                {
                    OverallStatus = "FAIL",
                    FaultAttribution = "SERVER_TIMEOUT",
                    Notes = $"JSON parse error: {ex.Message}"
                };
            }
        }

        public static DecoderTelemetrySample EvaluateDevice(VOIPS.DEVICE_OBJECT dev, string ip = "", Dictionary<string, string>? multicastToEncoderMap = null)
        {
            if (dev == null)
            {
                return new DecoderTelemetrySample
                {
                    OverallStatus = "FAIL",
                    FaultAttribution = "SERVER_TIMEOUT",
                    Notes = "Null Device Object"
                };
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dev);
            var jObj = JObject.Parse(json);
            return EvaluateParsedJObject(jObj, dev.device_id ?? "", ip, multicastToEncoderMap);
        }

        private static DecoderTelemetrySample EvaluateParsedJObject(JObject dev, string fallbackMac, string fallbackIp, Dictionary<string, string>? multicastToEncoderMap = null)
        {
            string mac = dev["device_id"]?.ToString() ?? fallbackMac;
            string name = dev["configuration"]?["device_name"]?.ToString() ?? mac;
            string devIp = dev["nodes"]?.FirstOrDefault(n => n["type"]?.ToString() == "NETWORK_INTERFACE")?["status"]?["ip"]?["address"]?.ToString()
                          ?? fallbackIp;

            var sample = new DecoderTelemetrySample
            {
                Timestamp = DateTime.Now,
                MacAddress = mac,
                DecoderName = name,
                IpAddress = devIp
            };

            // Device Status telemetry: temperature, error codes, active state
            var devStatus = dev["status"];
            if (devStatus != null)
            {
                sample.TemperatureC = devStatus["temperature"]?.Value<int>() ?? 0;
                sample.IsDeviceActive = devStatus["active"]?.Value<bool>() ?? true;
                var errObj = devStatus["error_status"];
                if (errObj != null)
                {
                    sample.HasInternalErrorCode = errObj["has_error_code"]?.Value<bool>() ?? false;
                    sample.InternalErrorCode = errObj["error_code"]?.Value<int>() ?? 0;
                }
            }

            // Network Interface telemetry: IP mode (DHCP vs Static), mask, gateway
            var devNodes = dev["nodes"] as JArray;
            var netIfNode = devNodes?.FirstOrDefault(n => n["type"]?.ToString() == "NETWORK_INTERFACE");
            if (netIfNode != null)
            {
                string ipMode = netIfNode["status"]?["ip"]?["mode"]?.ToString()
                             ?? netIfNode["configuration"]?["ip"]?["mode"]?.ToString()
                             ?? "UNKNOWN";
                sample.NetworkIpMode = ipMode.ToUpperInvariant();
                sample.SubnetMask = netIfNode["status"]?["ip"]?["mask"]?.ToString()
                                 ?? netIfNode["configuration"]?["ip"]?["mask"]?.ToString() ?? "";
                sample.GatewayIp = netIfNode["status"]?["ip"]?["gateway"]?.ToString()
                                ?? netIfNode["configuration"]?["ip"]?["gateway"]?.ToString() ?? "";
            }

            // =========================================================================
            // QUESTION 1 EVALUATION: Guarantee Video Received from Encoder
            // =========================================================================
            bool hasConfiguredRoute = false;
            bool isStreamingState = false;
            string multicastAddr = "0.0.0.0";
            string sourceTx = "N/A";

            var subs = dev["subscriptions"] as JArray;
            if (subs != null)
            {
                var hdmiSub = subs.FirstOrDefault(s => s["type"]?.ToString() == "HDMI" && s["index"]?.Value<int>() == 0)
                              ?? subs.FirstOrDefault(s => s["type"]?.ToString() == "HDMI");
                if (hdmiSub != null)
                {
                    multicastAddr = hdmiSub["configuration"]?["address"]?.ToString() ?? "0.0.0.0";
                    string subState = hdmiSub["status"]?["state"]?.ToString()?.ToUpperInvariant() ?? "STOPPED";
                    bool enable = hdmiSub["configuration"]?["enable"]?.Value<bool>() ?? (multicastAddr != "0.0.0.0");

                    // Check source transmitter info if present
                    var srcToken = hdmiSub["configuration"]?["source"];
                    if (srcToken is JValue srcVal)
                    {
                        sourceTx = srcVal.ToString();
                    }
                    else if (srcToken?["device_id"] != null)
                    {
                        sourceTx = srcToken["device_id"]!.ToString();
                    }

                    // Fallback: cross-reference multicast address from encoder discovery map
                    if ((sourceTx == "N/A" || string.IsNullOrWhiteSpace(sourceTx)) && multicastToEncoderMap != null && multicastToEncoderMap.TryGetValue(multicastAddr, out var mappedTxMac))
                    {
                        sourceTx = mappedTxMac;
                    }

                    hasConfiguredRoute = (enable && multicastAddr != "0.0.0.0") || sourceTx != "N/A";
                    isStreamingState = hasConfiguredRoute && (subState == "STREAMING" || subState == "ACTIVE" || subState == "CONNECTED");
                }
            }

            // Also check device-level streams or resume_streaming
            if (!isStreamingState && dev["streams"] is JArray devStreams && devStreams.Count > 0)
            {
                string sState = devStreams[0]["status"]?["state"]?.ToString()?.ToUpperInvariant() ?? "";
                if (sState == "STREAMING" || sState == "ACTIVE" || dev["configuration"]?["resume_streaming"]?.Value<bool>() == true)
                {
                    isStreamingState = true;
                    hasConfiguredRoute = true;
                }
            }

            sample.MulticastAddress = multicastAddr;
            sample.SourceEncoderMac = sourceTx;

            // Nodes inspection
            var nodes = dev["nodes"] as JArray;
            JToken? videoOutNode = null;
            if (nodes != null)
            {
                videoOutNode = nodes.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_ENCODER" && n["index"]?.Value<int>() == 0)
                               ?? nodes.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_ENCODER")
                               ?? nodes.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_DECODER");

                // Network Port Telemetry
                var netPortNode = nodes.FirstOrDefault(n => n["type"]?.ToString() == "NETWORK_PORT");
                if (netPortNode != null)
                {
                    sample.NetworkPortActive = netPortNode["status"]?["active"]?.Value<bool>() ?? true;
                    var speedVal = netPortNode["status"]?["Speed"];
                    sample.NetworkPortSpeed = speedVal != null ? $"{speedVal}G" : (sample.NetworkPortActive ? "10G" : "DOWN");
                }

                // Color Generator (Test Pattern) Telemetry
                var colorGenNode = nodes.FirstOrDefault(n => n["type"]?.ToString() == "COLOR_GENERATOR");
                if (colorGenNode != null)
                {
                    sample.IsColorGeneratorActive = colorGenNode["configuration"]?["enable"]?.Value<bool>() ?? false;
                }
            }

            bool clockStable = false;
            double fps = 0;
            long pixelClock = 0;
            int width = 0;
            int height = 0;
            string colorSpace = "";

            if (videoOutNode != null)
            {
                clockStable = videoOutNode["status"]?["source_stable"]?.Value<bool>() ?? false;
                sample.IsHdcpProtected = videoOutNode["status"]?["hdcp_protected"]?.Value<bool>() ?? false;

                var vObj = videoOutNode["status"]?["Video"];
                if (vObj != null)
                {
                    width = vObj["width"]?.Value<int>() ?? 0;
                    height = vObj["height"]?.Value<int>() ?? 0;
                    colorSpace = vObj["color_space"]?.ToString() ?? "";
                }

                var vDetails = videoOutNode["status"]?["video_details"];
                if (vDetails != null)
                {
                    fps = vDetails["frame_rate"]?.Value<double>() ?? (vDetails["fps"]?.Value<double>() ?? 0);
                    pixelClock = vDetails["pixel_clock"]?.Value<long>() ?? 0;
                    if (width == 0) width = vDetails["width"]?.Value<int>() ?? 0;
                    if (height == 0) height = vDetails["height"]?.Value<int>() ?? 0;
                }
            }

            // Synthesize pixel clock if missing in mock data but width/height/fps present
            if (pixelClock == 0 && width > 0 && height > 0)
            {
                pixelClock = (long)(width * height * (fps > 0 ? fps : 60.0));
            }

            // Check Multi-Link Dual-chip receiver (e.g. AVP2000 4K pair)
            var multiLinkNode = nodes?.FirstOrDefault(n => n["type"]?.ToString() == "MULTI_LINK_RECEIVER");
            bool isDualLink = false;
            if (multiLinkNode != null)
            {
                sample.MultiLinkMode = multiLinkNode["status"]?["link_mode"]?.ToString() ?? "SINGLE";
                sample.MultiLinkStatus = multiLinkNode["status"]?["link_status"]?.ToString() ?? "N/A";
                isDualLink = sample.MultiLinkMode == "DUAL";
            }

            if (isDualLink && width == 1920 && height == 2160)
            {
                width = 3840;
            }

            sample.IsClockStable = clockStable;
            sample.PixelClockMhz = Math.Round((double)pixelClock / 1000000.0, 1);

            if (width > 0 && height > 0)
            {
                sample.VideoRaster = $"{width}x{height}@{Math.Round(fps > 0 ? fps : 60.0)}Hz";
                sample.ColorFormat = !string.IsNullOrWhiteSpace(colorSpace) ? colorSpace : "";
            }
            else
            {
                sample.VideoRaster = "No Raster Detected";
            }

            // Conclude Question 1
            if (isStreamingState && clockStable && (pixelClock > 0 || width > 0))
            {
                sample.VideoReceivedStatus = "RECEIVED";
                sample.Question1Badge = "VIDEO RECEIVED";
                sample.Question1Details = $"Active stream ({multicastAddr}), Genlock locked, {sample.VideoRaster}";
            }
            else if (hasConfiguredRoute)
            {
                sample.VideoReceivedStatus = "NO_STREAM";
                sample.Question1Badge = "NO VIDEO STREAM";
                sample.Question1Details = $"Subscribed, but no video clock lock (source_stable={clockStable})";
            }
            else
            {
                sample.VideoReceivedStatus = "UNSUBSCRIBED";
                sample.Question1Badge = "UNSUBSCRIBED";
                sample.Question1Details = "Decoder has not joined an active encoder stream";
            }

            // =========================================================================
            // QUESTION 2 EVALUATION: Check if connected display is displaying or not
            // =========================================================================
            var monitorNode = nodes?.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_MONITOR" && n["index"]?.Value<int>() == 0)
                              ?? nodes?.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_MONITOR");

            bool isHpdConnected = false;
            JToken? edidToken = null;

            if (monitorNode != null)
            {
                isHpdConnected = monitorNode["status"]?["connected"]?.Value<bool>() ?? false;
                edidToken = monitorNode["status"]?["edid"];
            }

            sample.IsHpdConnected = isHpdConnected;
            sample.IsTmdsClockActive = (pixelClock > 0 || width > 0) && clockStable;

            if (!isHpdConnected)
            {
                sample.DisplayScreenStatus = "NO_DISPLAY";
                sample.DisplayModelName = "No Display Detected";
                sample.Question2Badge = "NO CABLE / DISPLAY OFF";
                sample.Question2Details = "HDMI HPD pin is low. Cable disconnected or monitor powered off.";
            }
            else if (edidToken == null)
            {
                // In mock/test environments without EDID property, default to generic HDMI display
                if (sample.VideoReceivedStatus == "RECEIVED")
                {
                    sample.DisplayScreenStatus = "DISPLAYING_VIDEO";
                    sample.DisplayModelName = "HDMI Display";
                    sample.Question2Badge = "DISPLAYING VIDEO";
                    sample.Question2Details = $"Displaying {sample.VideoRaster}";
                }
                else
                {
                    sample.DisplayScreenStatus = "WAITING_FOR_SOURCE";
                    sample.DisplayModelName = "HDMI Display";
                    sample.Question2Badge = "WAITING FOR SOURCE";
                    sample.Question2Details = "Display ready, waiting for video stream.";
                }
            }
            else
            {
                // Parse EDID
                string edidHex = edidToken.ToString();
                var edidInfo = EdidParser.Parse(edidHex);
                if (!edidInfo.IsValid)
                {
                    sample.DisplayScreenStatus = "HANDSHAKE_FAILED";
                    sample.DisplayModelName = "Display Unresponsive";
                    sample.Question2Badge = "HANDSHAKE FAILED";
                    sample.Question2Details = "HDMI cable connected, but monitor failed to return EDID handshake over DDC channel.";
                }
                else
                {
                    sample.DisplayModelName = edidInfo.ModelName;
                    sample.DisplaySerialNumber = edidInfo.SerialNumber;
                    sample.DisplayNativeTiming = $"{edidInfo.PreferredWidth}x{edidInfo.PreferredHeight}@{Math.Round(edidInfo.PreferredRefreshRate)}Hz";
                    sample.HasAudioSupport = edidInfo.HasAudioSupport;

                    if (sample.IsHdcpBlocked)
                    {
                        sample.DisplayScreenStatus = "BLACK_SCREEN";
                        sample.Question2Badge = "BLACK SCREEN (HDCP)";
                        sample.Question2Details = $"Display ({sample.DisplayModelName}) connected, but content is HDCP blocked.";
                    }
                    else if (sample.VideoReceivedStatus != "RECEIVED")
                    {
                        sample.DisplayScreenStatus = "WAITING_FOR_SOURCE";
                        sample.Question2Badge = "WAITING FOR SOURCE";
                        sample.Question2Details = $"Display ({sample.DisplayModelName}) ready. Waiting for video stream from encoder.";
                    }
                    else
                    {
                        sample.DisplayScreenStatus = "DISPLAYING_VIDEO";
                        sample.Question2Badge = "DISPLAYING VIDEO";
                        string serial = !string.IsNullOrWhiteSpace(sample.DisplaySerialNumber) ? $" [S/N: {sample.DisplaySerialNumber}]" : "";
                        sample.Question2Details = $"Actively displaying on {sample.DisplayModelName}{serial} via {sample.VideoRaster} TMDS clock.";
                    }
                }
            }

            ComputeOverall(sample);
            return sample;
        }

        private static void ComputeOverall(DecoderTelemetrySample sample)
        {
            var notes = new List<string>();

            // 0. Check for DHCP Fault (APIPA 169.254.x.x or unassigned 0.0.0.0 in DHCP mode)
            bool isDhcp = string.Equals(sample.NetworkIpMode, "DHCP", StringComparison.OrdinalIgnoreCase);
            bool isApipa = !string.IsNullOrWhiteSpace(sample.IpAddress) && sample.IpAddress.StartsWith("169.254.", StringComparison.OrdinalIgnoreCase);
            bool isZeroIp = string.IsNullOrWhiteSpace(sample.IpAddress) || sample.IpAddress == "0.0.0.0";

            if (isDhcp && (isApipa || isZeroIp))
            {
                sample.IsDhcpFault = true;
                sample.FaultAttribution = "DECODER_DHCP_FAULT";
                if (isApipa)
                {
                    notes.Add($"DHCP Lease Failure (APIPA fallback {sample.IpAddress})");
                }
                else
                {
                    notes.Add("DHCP Unassigned (No IP address)");
                }
            }

            // 1. Check for PLL Glitch / Anomalous Pixel Clock (~83.8% of normal 297MHz or 594MHz)
            bool isPllAnomalous = (sample.PixelClockMhz >= 240.0 && sample.PixelClockMhz <= 255.0)
                               || (sample.PixelClockMhz >= 490.0 && sample.PixelClockMhz <= 505.0)
                               || (sample.PixelClockMhz >= 390.0 && sample.PixelClockMhz <= 405.0);

            if (isPllAnomalous && sample.FaultAttribution == "NONE")
            {
                sample.FaultAttribution = "DECODER_PLL_DESYNC";
                notes.Add($"Decoder PLL Miscalculation Glitch ({sample.PixelClockMhz:F1}MHz ~83.8% lock)");
            }

            // 2. Check for Dual-Link Desynchronization
            if (sample.MultiLinkMode == "DUAL" && !string.Equals(sample.MultiLinkStatus, "SYNCHRONIZED", StringComparison.OrdinalIgnoreCase) && sample.MultiLinkStatus != "N/A" && sample.FaultAttribution == "NONE")
            {
                sample.FaultAttribution = "DECODER_DUAL_DESYNC";
                notes.Add($"Dual-Link Companion Desync ({sample.MultiLinkStatus})");
            }

            // 3. Check for Device Inactivity / Power Loss vs Network Link Loss
            if (!sample.IsDeviceActive && sample.FaultAttribution == "NONE")
            {
                if (!sample.IsHpdConnected)
                {
                    sample.IsPowerLoss = true;
                    sample.FaultAttribution = "DECODER_POWER_LOSS";
                    notes.Add("Decoder Sudden Power Loss (Device Inactive & Display HPD 0V)");
                }
                else
                {
                    sample.IsNetworkLinkDown = true;
                    sample.FaultAttribution = "NETWORK_LINK_DOWN";
                    notes.Add("Network Link Disconnected (Device Inactive, but Display HPD 5V Active)");
                }
            }

            // 4. Healthy State
            if (sample.VideoReceivedStatus == "RECEIVED" && sample.DisplayScreenStatus == "DISPLAYING_VIDEO" && !sample.IsDhcpFault && !sample.IsPowerLoss && !sample.IsNetworkLinkDown)
            {
                sample.OverallStatus = "PASS";
                if (sample.FaultAttribution == "NONE")
                {
                    sample.Notes = $"Healthy: Receiving stream from encoder & displaying on {sample.DisplayModelName}";
                }
                else
                {
                    sample.Notes = $"Recovered with Warning: {string.Join("; ", notes)}";
                }
                return;
            }

            // 5. Fault Attribution & Isolation
            if (sample.FaultAttribution == "NONE")
            {
                if (sample.DisplayScreenStatus == "NO_DISPLAY")
                {
                    sample.FaultAttribution = "DISPLAY_HPD_DOWN";
                    notes.Add("Display Disconnected / Sleep (HPD Low)");
                }
                else if (sample.DisplayScreenStatus == "HANDSHAKE_FAILED")
                {
                    sample.FaultAttribution = "DISPLAY_EDID_CORRUPT";
                    notes.Add("Display Handshake Failed (Bad EDID)");
                }
                else if (sample.DisplayScreenStatus == "BLACK_SCREEN")
                {
                    sample.FaultAttribution = "DISPLAY_HDCP_BLOCKED";
                    notes.Add("Black Screen (HDCP/Mute)");
                }
                else if (sample.VideoReceivedStatus == "NO_STREAM")
                {
                    sample.FaultAttribution = "SOURCE_REBOOTING";
                    notes.Add("No Video Stream from Encoder (Clock Unlocked)");
                }
                else if (sample.VideoReceivedStatus == "UNSUBSCRIBED")
                {
                    sample.FaultAttribution = "DECODER_STREAM_LOSS";
                    notes.Add("Decoder Unsubscribed");
                }
            }

            // High Thermal Warning
            if (sample.TemperatureC >= 68)
            {
                notes.Add($"High Chip Temperature ({sample.TemperatureC}°C)");
            }

            if (sample.DisplayScreenStatus == "NO_DISPLAY" ||
                sample.DisplayScreenStatus == "HANDSHAKE_FAILED" ||
                sample.VideoReceivedStatus == "NO_STREAM" ||
                sample.IsDhcpFault ||
                sample.IsPowerLoss ||
                sample.IsNetworkLinkDown ||
                !sample.IsDeviceActive)
            {
                sample.OverallStatus = "FAIL";
            }
            else
            {
                sample.OverallStatus = "WARN";
            }

            sample.Notes = string.Join("; ", notes);
        }

        /// <summary>
        /// Generates a diagnostic telemetry sample when the SDVoE Control Server cannot be queried for a device,
        /// isolating whether the failure is an abrupt decoder power loss, network cable disconnect, or server glitch.
        /// </summary>
        public static DecoderTelemetrySample CreateUnreachableSample(string mac, string name, string ip, bool isLanPingable, bool wasHpdConnected)
        {
            var sample = new DecoderTelemetrySample
            {
                Timestamp = DateTime.Now,
                MacAddress = mac,
                DecoderName = string.IsNullOrWhiteSpace(name) ? mac : name,
                IpAddress = ip,
                OverallStatus = "FAIL",
                IsDeviceActive = false,
                IsHpdConnected = wasHpdConnected && isLanPingable
            };

            if (isLanPingable)
            {
                sample.FaultAttribution = "SERVER_TIMEOUT";
                sample.Notes = "SDVoE Server Unresponsive (Device MCU is alive and responding on LAN)";
                sample.DisplayScreenStatus = wasHpdConnected ? "DISPLAYING_VIDEO" : "NO_DISPLAY";
            }
            else if (!wasHpdConnected)
            {
                sample.IsPowerLoss = true;
                sample.FaultAttribution = "DECODER_POWER_LOSS";
                sample.Notes = "Decoder Sudden Power Loss (Unreachable on LAN & Display HPD 0V)";
                sample.DisplayScreenStatus = "NO_DISPLAY";
            }
            else
            {
                sample.IsNetworkLinkDown = true;
                sample.FaultAttribution = "NETWORK_LINK_DOWN";
                sample.Notes = "Network Link Down (Device unreachable on LAN, Display HPD remained High)";
                sample.DisplayScreenStatus = "WAITING_FOR_SOURCE";
            }

            return sample;
        }

        /// <summary>
        /// Evaluates an SDVoE Transmitter (TX / Encoder) raw JSON payload and extracts hardware and video input telemetry.
        /// </summary>
        public static EncoderTelemetrySample EvaluateEncoder(string rawJson, EncoderItem encoder)
        {
            var sample = new EncoderTelemetrySample
            {
                EncoderName = encoder.DeviceName,
                MacAddress = encoder.MacAddress,
                IpAddress = encoder.IpAddress,
                Timestamp = DateTime.Now
            };

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                sample.OverallStatus = "FAIL";
                sample.Notes = "Empty response from control server";
                return sample;
            }

            try
            {
                var root = JObject.Parse(rawJson);
                var devices = root["result"]?["devices"] as JArray;
                if (devices == null || devices.Count == 0)
                {
                    sample.OverallStatus = "FAIL";
                    sample.Notes = "No device objects found in payload";
                    return sample;
                }

                // Match device by MAC or take first
                string cleanMac = encoder.MacAddress.Replace(":", "").Replace("-", "");
                var dev = devices.FirstOrDefault(d =>
                {
                    string id = d["device_id"]?.ToString()?.Replace(":", "")?.Replace("-", "") ?? "";
                    return string.Equals(id, cleanMac, StringComparison.OrdinalIgnoreCase);
                }) as JObject ?? devices[0] as JObject;

                if (dev == null)
                {
                    sample.OverallStatus = "FAIL";
                    sample.Notes = "Encoder device matching MAC not found";
                    return sample;
                }

                var devName = dev["configuration"]?["device_name"]?.ToString();
                if (!string.IsNullOrEmpty(devName)) sample.EncoderName = devName;

                // Temperature
                var tempToken = dev["status"]?["temperature"];
                if (tempToken != null && double.TryParse(tempToken.ToString(), out double tempVal))
                {
                    sample.TemperatureC = tempVal;
                }

                // Error Status
                var errObj = dev["status"]?["error_status"];
                if (errObj != null)
                {
                    sample.ErrorCode = errObj["code"]?.Value<int>() ?? 0;
                    sample.HasErrorCode = sample.ErrorCode != 0;
                }

                // Nodes: on TX transmitters, HDMI input from PC is HDMI_DECODER node
                var nodes = dev["nodes"] as JArray;
                if (nodes != null)
                {
                    var hdmiInNode = nodes.FirstOrDefault(n => n["type"]?.ToString() == "HDMI_DECODER");
                    if (hdmiInNode != null)
                    {
                        sample.IsSourceStable = hdmiInNode["status"]?["source_stable"]?.Value<bool>() ?? false;
                        sample.IsHdcpProtected = hdmiInNode["status"]?["hdcp_protected"]?.Value<bool>() ?? false;

                        int width = 0;
                        int height = 0;
                        var vObj = hdmiInNode["status"]?["Video"];
                        if (vObj != null)
                        {
                            width = vObj["width"]?.Value<int>() ?? 0;
                            height = vObj["height"]?.Value<int>() ?? 0;
                            sample.ColorFormat = vObj["color_space"]?.ToString() ?? "";
                        }

                        var vDetails = hdmiInNode["status"]?["video_details"];
                        double fps = 0;
                        if (vDetails != null)
                        {
                            width = vDetails["width"]?.Value<int>() ?? width;
                            height = vDetails["height"]?.Value<int>() ?? height;
                            fps = vDetails["fps"]?.Value<double>() ?? (vDetails["frame_rate"]?.Value<double>() ?? 0);
                            long pclk = vDetails["pixel_clock"]?.Value<long>() ?? 0;
                            if (pclk > 0)
                            {
                                sample.PixelClockMhz = Math.Round((double)pclk / 1_000_000.0, 2);
                            }
                        }

                        if (width > 0 && height > 0)
                        {
                            sample.VideoRaster = $"{width}x{height}@{(fps > 0 ? fps.ToString("0.##") : "60")}Hz";
                        }
                    }

                    // Multi-Link Transmitter Node
                    var multiLinkNode = nodes.FirstOrDefault(n => n["type"]?.ToString() == "MULTI_LINK_TRANSMITTER");
                    if (multiLinkNode != null)
                    {
                        sample.LinkMode = multiLinkNode["status"]?["link_mode"]?.ToString() ?? "SINGLE";
                        sample.LinkStatus = multiLinkNode["status"]?["link_status"]?.ToString() ?? "UNKNOWN";
                        var companions = multiLinkNode["configuration"]?["companions"] as JArray;
                        if (companions != null && companions.Count > 0)
                        {
                            sample.CompanionMac = companions[0].ToString();
                            encoder.CompanionMac = sample.CompanionMac;
                        }
                    }
                }

                // Streams: outbound multicast transmission to network
                var streams = dev["streams"] as JArray;
                if (streams != null)
                {
                    var hdmiStream = streams.FirstOrDefault(s => s["type"]?.ToString() == "HDMI" || s["index"]?.Value<int>() == 0);
                    if (hdmiStream != null)
                    {
                        sample.StreamState = hdmiStream["status"]?["state"]?.ToString()?.ToUpperInvariant() ?? "UNKNOWN";
                        sample.MulticastAddress = hdmiStream["configuration"]?["address"]?.ToString() ?? "0.0.0.0";
                        sample.IsStreaming = sample.StreamState == "STREAMING" || sample.StreamState == "ACTIVE";
                        encoder.MulticastAddress = sample.MulticastAddress;
                    }
                }

                // Determine HasVideo: requires source_stable && active pixel clock / raster && streaming
                sample.HasVideo = sample.IsSourceStable && (sample.PixelClockMhz > 0 || sample.VideoRaster.Contains("x")) && sample.IsStreaming;

                if (sample.HasVideo)
                {
                    sample.OverallStatus = "PASS";
                    sample.Notes = $"Healthy: Active HDMI input ({sample.VideoRaster}) streaming to {sample.MulticastAddress}";
                }
                else if (sample.IsSourceStable && !sample.IsStreaming)
                {
                    sample.OverallStatus = "WARN";
                    sample.Notes = "HDMI source stable, but stream output is stopped";
                }
                else
                {
                    sample.OverallStatus = "FAIL";
                    sample.Notes = "No video input from source (source_stable=false, source PC rebooting/disconnected)";
                }
            }
            catch (Exception ex)
            {
                sample.OverallStatus = "FAIL";
                sample.Notes = $"Parsing error: {ex.Message}";
            }

            return sample;
        }
    }
}
