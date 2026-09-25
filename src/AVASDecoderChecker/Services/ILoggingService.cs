using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public interface ILoggingService
    {
        string? CurrentSessionDirectory { get; }
        string? CurrentSessionId { get; }
        bool IsSessionActive { get; }

        Task<string> StartNewSessionAsync(string? baseLogsDirectory = null);
        Task LogSampleAsync(DecoderTelemetrySample sample);
        Task LogEncoderSampleAsync(EncoderTelemetrySample sample);
        Task LogAnomalyEventAsync(AnomalyEvent anomaly);
        Task<TestSessionSummary> ConcludeSessionAsync();
    }
}
