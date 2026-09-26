using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using AVASDecoderChecker.ViewModels;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = new AppSettings
        {
            ControlServerIp = "192.168.1.55",
            ControlServerPort = 6970,
            PollingIntervalSeconds = 1.0,
            SelectedDecoderMacs = new List<string> { "00:0e:c6:aa:bb:01" }
        };

        public string SettingsFilePath => "fake_settings.json";

        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(CurrentSettings);

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            return Task.CompletedTask;
        }
    }

    public class MainViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_LoadsSettingsProperly()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeLogger = new CsvLoggingService(Path.GetTempPath());
            var engine = new MonitorEngine(fakeSdvoe, fakeLogger);

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, engine);
            await vm.InitializeAsync();

            Assert.Equal("192.168.1.55", vm.ServerIp);
            Assert.Equal(6970, vm.ServerPort);
            Assert.Equal(1.0, vm.PollingInterval);
            Assert.Contains(vm.ActivityLogs, log => log.Contains("Settings loaded"));
        }

        [Fact]
        public void SelectAll_And_DeselectAll_UpdatesCountsAndCommands()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeLogger = new CsvLoggingService(Path.GetTempPath());
            var engine = new MonitorEngine(fakeSdvoe, fakeLogger);

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, engine);

            vm.Decoders.Add(new DecoderItem { MacAddress = "00:0e:c6:11:11:11", DeviceName = "RX1", IsSelected = false });
            vm.Decoders.Add(new DecoderItem { MacAddress = "00:0e:c6:22:22:22", DeviceName = "RX2", IsSelected = false });

            Assert.Equal(0, vm.SelectedDecodersCount);
            Assert.False(vm.CanStartTest);

            vm.SelectAllCommand.Execute(null);

            Assert.Equal(2, vm.SelectedDecodersCount);
            Assert.True(vm.CanStartTest);

            vm.DeselectAllCommand.Execute(null);

            Assert.Equal(0, vm.SelectedDecodersCount);
            Assert.False(vm.CanStartTest);
        }

        [Fact]
        public async Task SaveSettingsAndVerifyAsync_PersistsSettings()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeLogger = new CsvLoggingService(Path.GetTempPath());
            var engine = new MonitorEngine(fakeSdvoe, fakeLogger);

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, engine);
            vm.ServerIp = "10.0.0.99";
            vm.ServerPort = 8080;
            vm.PollingInterval = 3.5;

            await vm.SaveSettingsAndVerifyAsync();

            Assert.Equal("10.0.0.99", fakeSettings.CurrentSettings.ControlServerIp);
            Assert.Equal(8080, fakeSettings.CurrentSettings.ControlServerPort);
            Assert.Equal(3.5, fakeSettings.CurrentSettings.PollingIntervalSeconds);
            Assert.Equal("SUCCESS", vm.ConnectionStatusLevel);
            Assert.True(vm.IsSettingsLocked);
            Assert.False(vm.CanEditServerSettings);
            Assert.True(vm.HasConnectionBanner);
            Assert.Equal("SUCCESS", vm.ConnectionBannerLevel);
            Assert.True(vm.Decoders.Count > 0);
        }

        [Fact]
        public async Task UnlockSettings_UnlocksServerFieldsAndUpdatesBanner()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeLogger = new CsvLoggingService(Path.GetTempPath());
            var engine = new MonitorEngine(fakeSdvoe, fakeLogger);

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, engine);
            await vm.SaveSettingsAndVerifyAsync();

            Assert.True(vm.IsSettingsLocked);

            vm.UnlockSettingsCommand.Execute(null);

            Assert.False(vm.IsSettingsLocked);
            Assert.True(vm.CanEditServerSettings);
            Assert.Equal("INFO", vm.ConnectionBannerLevel);
        }

        [Fact]
        public void TelemetrySampleReceived_UpdatesQ1AndQ2KpiCounters_AndSelectedRow()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeEngine = new FakeMonitorEngine();

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, fakeEngine);

            // Set a selected row
            var selectedRow = new DecoderTelemetrySample
            {
                MacAddress = "00:0e:c6:aa:bb:01",
                DecoderName = "Decoder-1",
                VideoReceivedStatus = "NO STREAM",
                DisplayScreenStatus = "NO CABLE / OFF"
            };
            vm.SelectedTelemetryRow = selectedRow;

            // Fire an updated sample for the same MAC
            fakeEngine.FireSample(new DecoderTelemetrySample
            {
                MacAddress = "00:0e:c6:aa:bb:01",
                DecoderName = "Decoder-1",
                OverallStatus = "PASS",
                VideoReceivedStatus = "RECEIVING",
                DisplayScreenStatus = "DISPLAYING",
                DisplayModelName = "ViewSonic VP3268-4K"
            });

            // Fire a sample for another MAC with issues
            fakeEngine.FireSample(new DecoderTelemetrySample
            {
                MacAddress = "00:0e:c6:aa:bb:02",
                DecoderName = "Decoder-2",
                OverallStatus = "FAIL",
                VideoReceivedStatus = "NO STREAM",
                DisplayScreenStatus = "NO CABLE / OFF"
            });

            Assert.Equal(2, vm.KpiTotalSamples);
            Assert.Equal(1, vm.KpiPassingCount);
            Assert.Equal(1, vm.KpiIssuesCount);
            Assert.Equal(50.0, vm.KpiHealthPercentage);

            // Q1 KPIs
            Assert.Equal(1, vm.KpiVideoReceivedCount);
            Assert.Equal(1, vm.KpiNoVideoCount);

            // Q2 KPIs
            Assert.Equal(1, vm.KpiDisplaysActiveCount);
            Assert.Equal(1, vm.KpiDisplaysIssueCount);

            // Selected row automatically updated
            Assert.NotNull(vm.SelectedTelemetryRow);
            Assert.Equal("00:0e:c6:aa:bb:01", vm.SelectedTelemetryRow.MacAddress);
            Assert.Equal("RECEIVING", vm.SelectedTelemetryRow.VideoReceivedStatus);
            Assert.Equal("DISPLAYING", vm.SelectedTelemetryRow.DisplayScreenStatus);
            Assert.Equal("ViewSonic VP3268-4K", vm.SelectedTelemetryRow.DisplayModelName);
        }

        [Fact]
        public void EncoderTelemetrySampleReceived_UpdatesEncoderProperties()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeEngine = new FakeMonitorEngine();

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, fakeEngine);

            fakeEngine.FireEncoderSample(new EncoderTelemetrySample
            {
                EncoderName = "TX-Main",
                MacAddress = "74:fe:48:8b:07:bb",
                OverallStatus = "PASS",
                HasVideo = true,
                IsSourceStable = true,
                VideoRaster = "3840x2160@60Hz",
                TemperatureC = 53.2
            });

            Assert.Equal("PASS", vm.EncoderOverallStatus);
            Assert.Equal("74:fe:48:8b:07:bb", vm.EncoderMacText);
            Assert.Equal("3840x2160@60Hz", vm.EncoderRasterText);
            Assert.Equal("53.2°C", vm.EncoderTempText);
            Assert.Contains("ACTIVE", vm.EncoderStatusText);
            Assert.Contains("3840x2160@60Hz", vm.EncoderStatusText);
        }

        [Fact]
        public void AnomalyDetected_IncrementsKpiAnomalyCount_AndLogsIncident()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeEngine = new FakeMonitorEngine();

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, fakeEngine);

            Assert.Equal(0, vm.KpiAnomalyCount);

            fakeEngine.FireAnomaly(new AnomalyEvent
            {
                Timestamp = DateTime.Now,
                EventType = AnomalyEventType.POST_REBOOT_TIMEOUT,
                EncoderMac = "74:fe:48:8b:07:bb",
                AffectedDecoderMac = "74:fe:48:8b:07:81",
                AffectedDecoderName = "Decoder-Right",
                DurationSeconds = 2.8,
                Description = "Decoder failed to display video after 2.8s post-reboot (>2.0s window)"
            });

            Assert.Equal(1, vm.KpiAnomalyCount);
            Assert.NotEmpty(vm.ActivityLogs);
            Assert.Contains(vm.ActivityLogs, log => log.Contains("POST_REBOOT_TIMEOUT") && log.Contains("Decoder-Right"));
        }

        [Fact]
        public void AppVersion_AndUiProperties_AreConsistentWithAssembly()
        {
            var fakeSettings = new FakeSettingsService();
            var fakeSdvoe = new FakeSdvoeService();
            var fakeLogger = new CsvLoggingService(Path.GetTempPath());
            var engine = new MonitorEngine(fakeSdvoe, fakeLogger);

            var vm = new MainViewModel(fakeSettings, fakeSdvoe, engine);

            Assert.Equal("1.0.3", vm.AppVersion);
            Assert.Equal("v1.0.3", vm.AppVersionBadge);
            Assert.Equal("AVAS SDVoE Decoder Stream Checker (v1.0.3)", vm.WindowTitle);
            Assert.Equal("AVAS Decoder Checker v1.0.3 (.NET 8.0)", vm.AppFooterText);
        }
    }

    public class FakeMonitorEngine : IMonitorEngine
    {
        public bool IsRunning { get; set; }
        public string? CurrentSessionDirectory { get; set; }
        public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;

        public event EventHandler<DecoderTelemetrySample>? TelemetrySampleReceived;
        public event EventHandler<EncoderTelemetrySample>? EncoderTelemetrySampleReceived;
        public event EventHandler<AnomalyEvent>? AnomalyDetected;
        public event EventHandler<TestSessionSummary>? SessionCompleted;
        public event EventHandler<string>? StatusMessageLogged;

        public void FireEncoderSample(EncoderTelemetrySample sample)
        {
            EncoderTelemetrySampleReceived?.Invoke(this, sample);
        }

        public void FireAnomaly(AnomalyEvent anomaly)
        {
            AnomalyDetected?.Invoke(this, anomaly);
        }

        public Task<string> StartAsync(AppSettings settings, List<DecoderItem> selectedDecoders, string? baseLogsDir = null)
        {
            IsRunning = true;
            CurrentSessionDirectory = "fake_session_dir";
            return Task.FromResult(CurrentSessionDirectory);
        }

        public Task<TestSessionSummary?> StopAsync()
        {
            IsRunning = false;
            return Task.FromResult<TestSessionSummary?>(new TestSessionSummary());
        }

        public void FireSample(DecoderTelemetrySample sample)
        {
            TelemetrySampleReceived?.Invoke(this, sample);
        }
    }
}
