using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public interface IMonitorEngine
    {
        bool IsRunning { get; }
        string? CurrentSessionDirectory { get; }
        TimeSpan ElapsedTime { get; }

        event EventHandler<DecoderTelemetrySample>? TelemetrySampleReceived;
        event EventHandler<EncoderTelemetrySample>? EncoderTelemetrySampleReceived;
        event EventHandler<AnomalyEvent>? AnomalyDetected;
        event EventHandler<TestSessionSummary>? SessionCompleted;
        event EventHandler<string>? StatusMessageLogged;

        Task<string> StartAsync(AppSettings settings, List<DecoderItem> selectedDecoders, string? baseLogsDir = null);
        Task<TestSessionSummary?> StopAsync();
    }
}
