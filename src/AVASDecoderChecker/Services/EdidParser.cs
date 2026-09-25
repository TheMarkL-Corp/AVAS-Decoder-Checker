using System;
using System.Text;

namespace AVASDecoderChecker.Services
{
    public class EdidInfo
    {
        public bool IsValid { get; set; }
        public string ManufacturerCode { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string ModelName { get; set; } = "Unknown Display";
        public string SerialNumber { get; set; } = string.Empty;
        public int PreferredWidth { get; set; }
        public int PreferredHeight { get; set; }
        public double PreferredRefreshRate { get; set; }
        public bool HasAudioSupport { get; set; }
        public string RawHex { get; set; } = string.Empty;

        public string SummaryText
        {
            get
            {
                if (!IsValid) return "No display connected / unreadable EDID";
                string res = PreferredWidth > 0 && PreferredHeight > 0
                    ? $"{PreferredWidth}x{PreferredHeight}@{Math.Round(PreferredRefreshRate)}Hz"
                    : "";
                string serial = !string.IsNullOrWhiteSpace(SerialNumber) ? $" (S/N: {SerialNumber})" : "";
                return $"{ModelName}{serial} {res}".Trim();
            }
        }
    }

    public static class EdidParser
    {
        public static EdidInfo Parse(string? hexEdid)
        {
            var info = new EdidInfo();
            if (string.IsNullOrWhiteSpace(hexEdid)) return info;

            try
            {
                byte[] raw = HexToBytes(hexEdid.Trim());
                info.RawHex = hexEdid;

                if (raw.Length < 128) return info;

                // Check EDID Header: 00 FF FF FF FF FF FF 00
                if (raw[0] != 0x00 || raw[1] != 0xFF || raw[2] != 0xFF || raw[3] != 0xFF ||
                    raw[4] != 0xFF || raw[5] != 0xFF || raw[6] != 0xFF || raw[7] != 0x00)
                {
                    return info;
                }

                info.IsValid = true;

                // Manufacturer ID (Bytes 8-9)
                int mfgBits = (raw[8] << 8) | raw[9];
                char c1 = (char)('A' + ((mfgBits >> 10) & 0x1F) - 1);
                char c2 = (char)('A' + ((mfgBits >> 5) & 0x1F) - 1);
                char c3 = (char)('A' + (mfgBits & 0x1F) - 1);
                info.ManufacturerCode = $"{c1}{c2}{c3}".ToUpperInvariant();
                info.ManufacturerName = LookupManufacturer(info.ManufacturerCode);

                // Parse 4 Descriptors at 54, 72, 90, 108
                for (int offset = 54; offset <= 108; offset += 18)
                {
                    if (offset + 18 > raw.Length) break;

                    if (raw[offset] == 0x00 && raw[offset + 1] == 0x00)
                    {
                        // Display Descriptor
                        byte tag = raw[offset + 3];
                        if (tag == 0xFC) // Monitor Name
                        {
                            string name = ExtractAsciiString(raw, offset + 5, 13);
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                info.ModelName = name;
                            }
                        }
                        else if (tag == 0xFF) // Monitor Serial Number
                        {
                            string sn = ExtractAsciiString(raw, offset + 5, 13);
                            if (!string.IsNullOrWhiteSpace(sn))
                            {
                                info.SerialNumber = sn;
                            }
                        }
                    }
                    else if (info.PreferredWidth == 0) // First Detailed Timing Descriptor = Preferred Timing
                    {
                        int pixelClock10k = raw[offset] | (raw[offset + 1] << 8);
                        long pixelClockHz = pixelClock10k * 10000L;

                        int hActive = raw[offset + 2] | ((raw[offset + 4] & 0xF0) << 4);
                        int hBlank = raw[offset + 3] | ((raw[offset + 4] & 0x0F) << 8);
                        int hTotal = hActive + hBlank;

                        int vActive = raw[offset + 5] | ((raw[offset + 7] & 0xF0) << 4);
                        int vBlank = raw[offset + 6] | ((raw[offset + 7] & 0x0F) << 8);
                        int vTotal = vActive + vBlank;

                        if (hActive > 0 && vActive > 0)
                        {
                            info.PreferredWidth = hActive;
                            info.PreferredHeight = vActive;
                            if (hTotal > 0 && vTotal > 0 && pixelClockHz > 0)
                            {
                                info.PreferredRefreshRate = Math.Round((double)pixelClockHz / (hTotal * vTotal), 1);
                            }
                        }
                    }
                }

                // If ModelName wasn't in 0xFC descriptor, fallback to Manufacturer + Code
                if (info.ModelName == "Unknown Display" && !string.IsNullOrWhiteSpace(info.ManufacturerName))
                {
                    info.ModelName = $"{info.ManufacturerName} Display";
                }

                // Check CEA-861 Extension for Audio (typically Block 1, byte 128+)
                if (raw.Length >= 256 && raw[128] == 0x02)
                {
                    byte b3 = raw[131];
                    // Bit 6 of byte 3 in CEA extension indicates Basic Audio support
                    info.HasAudioSupport = (b3 & 0x40) != 0;
                }
            }
            catch
            {
                info.IsValid = false;
            }

            return info;
        }

        private static string ExtractAsciiString(byte[] data, int start, int maxLen)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < maxLen && (start + i) < data.Length; i++)
            {
                byte b = data[start + i];
                if (b == 0x0A || b == 0x00) break; // Terminating character
                if (b >= 32 && b <= 126) sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 != 0) hex = hex[..^1];
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static string LookupManufacturer(string code)
        {
            return code.ToUpperInvariant() switch
            {
                "VSC" => "ViewSonic",
                "SAM" => "Samsung",
                "DEL" => "Dell",
                "LGX" or "LGD" => "LG",
                "SNY" => "Sony",
                "NEC" => "NEC",
                "PAN" or "MEI" => "Panasonic",
                "BEN" => "BenQ",
                "ACR" => "Acer",
                "ASU" => "ASUS",
                "HWP" => "HP",
                "AOC" => "AOC",
                "IVM" => "Iiyama",
                _ => code
            };
        }
    }
}
