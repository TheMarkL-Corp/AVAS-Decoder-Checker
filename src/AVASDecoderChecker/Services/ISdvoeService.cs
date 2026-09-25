using System.Collections.Generic;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public interface ISdvoeService
    {
        Task<(bool Success, string Message, long LatencyMs, int DetectedPort)> TestConnectionAsync(string ip, int port, int timeoutMs = 3000);
        Task<List<DecoderItem>> QueryDecodersAsync(string ip, int port, int timeoutMs = 5000);
        Task<DecoderTelemetrySample> SampleDecoderTelemetryAsync(string ip, int port, DecoderItem decoder, int timeoutMs = 3000);
        Task<List<EncoderItem>> QueryEncodersAsync(string ip, int port, int timeoutMs = 5000);
        Task<EncoderTelemetrySample> SampleEncoderTelemetryAsync(string ip, int port, EncoderItem encoder, int timeoutMs = 3000);
    }
}
