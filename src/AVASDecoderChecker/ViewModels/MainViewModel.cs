using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;

namespace AVASDecoderChecker.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ISdvoeService _sdvoeService;
        private readonly IMonitorEngine _monitorEngine;

        private AppSettings _settings = new();
        private readonly DispatcherTimer _uiTimer;

        // Settings Fields
        private string _serverIp = "127.0.0.1";
        private int _serverPort = 8090;
        private int _timeoutMs = 3000;
        private double _pollingInterval = 2.0;
        private int _durationMinutes = 0;
        private bool _isSettingsLocked = false;

        // In-Card Visual Status Banner Fields
        private bool _hasConnectionBanner = false;
        private string _connectionBannerTitle = string.Empty;
        private string _connectionBannerDetails = string.Empty;
        private string _connectionBannerLevel = "IDLE"; // IDLE, SUCCESS, ERROR, WARN, INFO

        // Status Fields
        private string _connectionStatusText = "Awaiting Server Verification";
        private string _connectionStatusLevel = "IDLE"; // IDLE, INFO, SUCCESS, ERROR
        private bool _isConnectionOk = false;
        private bool _isBusy = false;
        private string _busyMessage = string.Empty;

        // Decoder Selection Fields
        private string _searchText = string.Empty;
        private ICollectionView? _decodersView;

        // Test Control Fields
        private bool _isTestRunning = false;
        private string _testStatusText = "IDLE";
        private string _elapsedTimeText = "00:00:00";
        private string _currentSessionDir = string.Empty;

        // KPI Summary Counters
        private int _kpiMonitoredDecoders = 0;
        private int _kpiTotalSamples = 0;
        private int _kpiPassingCount = 0;
        private int _kpiIssuesCount = 0;
        private int _kpiAnomalyCount = 0;
        private double _kpiHealthPercentage = 100.0;
        private int _kpiVideoReceivedCount = 0;
        private int _kpiNoVideoCount = 0;
        private int _kpiDisplaysActiveCount = 0;
        private int _kpiDisplaysIssueCount = 0;
        private DecoderTelemetrySample? _selectedTelemetryRow;

        // Upstream Transmitter (TX) Status Fields
        private string _encoderStatusText = "TX Offline / Undetected";
        private string _encoderRasterText = "--";
        private string _encoderTempText = "--";
        private string _encoderMacText = "--";
        private string _encoderOverallStatus = "UNKNOWN";

        // Collections
        public ObservableCollection<DecoderItem> Decoders { get; } = new();
        public ObservableCollection<DecoderTelemetrySample> LiveTelemetry { get; } = new();
        public ObservableCollection<string> ActivityLogs { get; } = new();

        #region Public Properties

        public string ServerIp
        {
            get => _serverIp;
            set => SetProperty(ref _serverIp, value);
        }

        public int ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        public int TimeoutMs
        {
            get => _timeoutMs;
            set => SetProperty(ref _timeoutMs, value);
        }

        public double PollingInterval
        {
            get => _pollingInterval;
            set => SetProperty(ref _pollingInterval, value);
        }

        public int DurationMinutes
        {
            get => _durationMinutes;
            set => SetProperty(ref _durationMinutes, value);
        }

        public bool IsSettingsLocked
        {
            get => _isSettingsLocked;
            set
            {
                if (SetProperty(ref _isSettingsLocked, value))
                {
                    OnPropertyChanged(nameof(CanEditServerSettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                }
            }
        }

        public bool CanEditServerSettings => !IsSettingsLocked && !IsTestRunning;
        public bool CanSaveSettings => !IsSettingsLocked && !IsTestRunning;

        public bool HasConnectionBanner
        {
            get => _hasConnectionBanner;
            set => SetProperty(ref _hasConnectionBanner, value);
        }

        public string ConnectionBannerTitle
        {
            get => _connectionBannerTitle;
            set => SetProperty(ref _connectionBannerTitle, value);
        }

        public string ConnectionBannerDetails
        {
            get => _connectionBannerDetails;
            set => SetProperty(ref _connectionBannerDetails, value);
        }

        public string ConnectionBannerLevel
        {
            get => _connectionBannerLevel;
            set => SetProperty(ref _connectionBannerLevel, value);
        }

        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
        }

        public string ConnectionStatusLevel
        {
            get => _connectionStatusLevel;
            set => SetProperty(ref _connectionStatusLevel, value);
        }

        public bool IsConnectionOk
        {
            get => _isConnectionOk;
            set
            {
                if (SetProperty(ref _isConnectionOk, value))
                {
                    OnPropertyChanged(nameof(CanStartTest));
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _decodersView?.Refresh();
                }
            }
        }

        public bool IsTestRunning
        {
            get => _isTestRunning;
            set
            {
                if (SetProperty(ref _isTestRunning, value))
                {
                    OnPropertyChanged(nameof(CanStartTest));
                    OnPropertyChanged(nameof(CanStopTest));
                    OnPropertyChanged(nameof(CanEditSettings));
                    OnPropertyChanged(nameof(CanEditServerSettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                }
            }
        }

        public bool CanStartTest => !IsTestRunning && SelectedDecodersCount > 0;
        public bool CanStopTest => IsTestRunning;
        public bool CanEditSettings => !IsTestRunning;

        public string TestStatusText
        {
            get => _testStatusText;
            set => SetProperty(ref _testStatusText, value);
        }

        public string ElapsedTimeText
        {
            get => _elapsedTimeText;
            set => SetProperty(ref _elapsedTimeText, value);
        }

        public string CurrentSessionDir
        {
            get => _currentSessionDir;
            set => SetProperty(ref _currentSessionDir, value);
        }

        public int SelectedDecodersCount => Decoders.Count(d => d.IsSelected);
        public int TotalDecodersCount => Decoders.Count;

        public int KpiMonitoredDecoders
        {
            get => _kpiMonitoredDecoders;
            set => SetProperty(ref _kpiMonitoredDecoders, value);
        }

        public int KpiTotalSamples
        {
            get => _kpiTotalSamples;
            set => SetProperty(ref _kpiTotalSamples, value);
        }

        public int KpiPassingCount
        {
            get => _kpiPassingCount;
            set => SetProperty(ref _kpiPassingCount, value);
        }

        public int KpiIssuesCount
        {
            get => _kpiIssuesCount;
            set => SetProperty(ref _kpiIssuesCount, value);
        }

        public double KpiHealthPercentage
        {
            get => _kpiHealthPercentage;
            set => SetProperty(ref _kpiHealthPercentage, value);
        }

        public int KpiVideoReceivedCount
        {
            get => _kpiVideoReceivedCount;
            set => SetProperty(ref _kpiVideoReceivedCount, value);
        }

        public int KpiNoVideoCount
        {
            get => _kpiNoVideoCount;
            set => SetProperty(ref _kpiNoVideoCount, value);
        }

        public int KpiDisplaysActiveCount
        {
            get => _kpiDisplaysActiveCount;
            set => SetProperty(ref _kpiDisplaysActiveCount, value);
        }

        public int KpiDisplaysIssueCount
        {
            get => _kpiDisplaysIssueCount;
            set => SetProperty(ref _kpiDisplaysIssueCount, value);
        }

        public int KpiAnomalyCount
        {
            get => _kpiAnomalyCount;
            set => SetProperty(ref _kpiAnomalyCount, value);
        }

        public string EncoderStatusText
        {
            get => _encoderStatusText;
            set => SetProperty(ref _encoderStatusText, value);
        }

        public string EncoderRasterText
        {
            get => _encoderRasterText;
            set => SetProperty(ref _encoderRasterText, value);
        }

        public string EncoderTempText
        {
            get => _encoderTempText;
            set => SetProperty(ref _encoderTempText, value);
        }

        public string EncoderMacText
        {
            get => _encoderMacText;
            set => SetProperty(ref _encoderMacText, value);
        }

        public string EncoderOverallStatus
        {
            get => _encoderOverallStatus;
            set => SetProperty(ref _encoderOverallStatus, value);
        }

        public DecoderTelemetrySample? SelectedTelemetryRow
        {
            get => _selectedTelemetryRow;
            set => SetProperty(ref _selectedTelemetryRow, value);
        }

        #endregion

        #region Commands

        public RelayCommand TestConnectionCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }
        public RelayCommand UnlockSettingsCommand { get; }
        public RelayCommand QueryDecodersCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand DeselectAllCommand { get; }
        public RelayCommand StartTestCommand { get; }
        public RelayCommand StopTestCommand { get; }
        public RelayCommand OpenLogsFolderCommand { get; }
        public RelayCommand ClearLogsCommand { get; }

        #endregion

        public MainViewModel(ISettingsService settingsService, ISdvoeService sdvoeService, IMonitorEngine monitorEngine)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _sdvoeService = sdvoeService ?? throw new ArgumentNullException(nameof(sdvoeService));
            _monitorEngine = monitorEngine ?? throw new ArgumentNullException(nameof(monitorEngine));

            // Setup Filter View
            _decodersView = CollectionViewSource.GetDefaultView(Decoders);
            _decodersView.Filter = FilterDecoders;

            // Wire Monitor Engine Events
            _monitorEngine.TelemetrySampleReceived += OnTelemetrySampleReceived;
            _monitorEngine.EncoderTelemetrySampleReceived += OnEncoderTelemetryReceived;
            _monitorEngine.AnomalyDetected += OnAnomalyDetected;
            _monitorEngine.SessionCompleted += OnSessionCompleted;
            _monitorEngine.StatusMessageLogged += (s, msg) => AddActivityLog(msg);

            // Setup UI Dispatcher Timer for elapsed time
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += (s, e) =>
            {
                if (IsTestRunning)
                {
                    ElapsedTimeText = _monitorEngine.ElapsedTime.ToString(@"hh\:mm\:ss");
                }
            };

            // Initialize Commands
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy && !IsTestRunning);
            SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAndVerifyAsync(), () => CanSaveSettings && !IsBusy);
            UnlockSettingsCommand = new RelayCommand(UnlockSettings, () => IsSettingsLocked && !IsTestRunning);
            QueryDecodersCommand = new RelayCommand(async () => await QueryDecodersAsync(), () => !IsBusy && !IsTestRunning);
            SelectAllCommand = new RelayCommand(SelectAllDecoders, () => Decoders.Count > 0 && !IsTestRunning);
            DeselectAllCommand = new RelayCommand(DeselectAllDecoders, () => Decoders.Count > 0 && !IsTestRunning);
            StartTestCommand = new RelayCommand(async () => await StartTestAsync(), () => CanStartTest && !IsBusy);
            StopTestCommand = new RelayCommand(async () => await StopTestAsync(), () => CanStopTest && !IsBusy);
            OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
            ClearLogsCommand = new RelayCommand(() => ActivityLogs.Clear());
        }

        public async Task InitializeAsync()
        {
            try
            {
                _settings = await _settingsService.LoadSettingsAsync();
                ServerIp = _settings.ControlServerIp;
                ServerPort = _settings.ControlServerPort > 0 ? _settings.ControlServerPort : 8090;
                TimeoutMs = _settings.TimeoutMs;
                PollingInterval = _settings.PollingIntervalSeconds;
                DurationMinutes = _settings.DurationMinutes;

                AddActivityLog($"Settings loaded. Configured Control Server: {ServerIp}:{ServerPort}");

                // Auto-test connection on initial load
                await TestConnectionAsync(isSilentOnSuccess: true);
            }
            catch (Exception ex)
            {
                AddActivityLog($"Error loading initial settings: {ex.Message}");
            }
        }

        public async Task TestConnectionAsync(bool isSilentOnSuccess = false)
        {
            IsBusy = true;
            BusyMessage = $"Testing connection to {ServerIp}:{ServerPort}...";
            ConnectionStatusText = "Connecting...";
            ConnectionStatusLevel = "INFO";
            ConnectionBannerTitle = "Testing Connection...";
            ConnectionBannerDetails = $"Sending discovery ping to {ServerIp}:{ServerPort}...";
            ConnectionBannerLevel = "INFO";
            HasConnectionBanner = true;
            AddActivityLog($"Testing connection to SDVoE Control Server ({ServerIp}:{ServerPort})...");

            try
            {
                var result = await _sdvoeService.TestConnectionAsync(ServerIp, ServerPort, TimeoutMs);
                if (result.Success)
                {
                    if (result.DetectedPort != ServerPort && result.DetectedPort > 0)
                    {
                        ServerPort = result.DetectedPort;
                    }

                    IsConnectionOk = true;
                    ConnectionStatusLevel = "SUCCESS";
                    ConnectionStatusText = $"Connected ({result.LatencyMs}ms)";
                    ConnectionBannerTitle = "✓ Server Connected Successfully";
                    ConnectionBannerDetails = $"{result.Message} (Latency: {result.LatencyMs}ms). Click 'Save & Verify' to lock settings and auto-load decoders.";
                    ConnectionBannerLevel = "SUCCESS";
                    AddActivityLog($"[SUCCESS] {result.Message} in {result.LatencyMs}ms");

                    // If decoders list is empty, query network decoders immediately
                    if (Decoders.Count == 0)
                    {
                        await QueryDecodersInternalAsync();
                    }
                }
                else
                {
                    IsConnectionOk = false;
                    ConnectionStatusLevel = "ERROR";
                    ConnectionStatusText = "Connection Failed";
                    ConnectionBannerTitle = "✕ Connection Failed";
                    ConnectionBannerDetails = result.Message;
                    ConnectionBannerLevel = "ERROR";
                    AddActivityLog($"[FAILED] {result.Message}");

                    if (!isSilentOnSuccess)
                    {
                        MessageBox.Show(result.Message, "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                IsConnectionOk = false;
                ConnectionStatusLevel = "ERROR";
                ConnectionStatusText = "Error";
                ConnectionBannerTitle = "✕ Connection Error";
                ConnectionBannerDetails = ex.Message;
                ConnectionBannerLevel = "ERROR";
                AddActivityLog($"[ERROR] Connection test failed: {ex.Message}");

                if (!isSilentOnSuccess)
                {
                    MessageBox.Show($"Connection test error: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SaveSettingsAndVerifyAsync()
        {
            IsBusy = true;
            BusyMessage = "Verifying SDVoE server and saving settings...";
            AddActivityLog($"Verifying SDVoE server at {ServerIp}:{ServerPort} before saving...");

            try
            {
                // Step 1: Verify connectivity first
                var result = await _sdvoeService.TestConnectionAsync(ServerIp, ServerPort, TimeoutMs);
                if (result.Success)
                {
                    if (result.DetectedPort != ServerPort && result.DetectedPort > 0)
                    {
                        ServerPort = result.DetectedPort;
                    }

                    IsConnectionOk = true;
                    IsSettingsLocked = true;
                    ConnectionStatusLevel = "SUCCESS";
                    ConnectionStatusText = $"Verified & Connected ({result.LatencyMs}ms)";

                    // Step 2: Query network decoders right after save & verify
                    await QueryDecodersInternalAsync();

                    // Step 3: Persist settings to JSON
                    _settings.ControlServerIp = ServerIp;
                    _settings.ControlServerPort = ServerPort;
                    _settings.TimeoutMs = TimeoutMs;
                    _settings.PollingIntervalSeconds = PollingInterval;
                    _settings.DurationMinutes = DurationMinutes;
                    _settings.SelectedDecoderMacs = Decoders.Where(d => d.IsSelected).Select(d => d.MacAddress).ToList();

                    await _settingsService.SaveSettingsAsync(_settings);
                    AddActivityLog("Settings saved successfully to config/settings.json.");

                    ConnectionBannerTitle = "✓ Server Verified & Settings Locked";
                    ConnectionBannerDetails = $"{result.Message} Discovered {Decoders.Count} decoder(s). Settings are locked for testing.";
                    ConnectionBannerLevel = "SUCCESS";
                    HasConnectionBanner = true;
                }
                else
                {
                    IsConnectionOk = false;
                    IsSettingsLocked = false;
                    ConnectionStatusLevel = "ERROR";
                    ConnectionStatusText = "Server Unreachable";
                    ConnectionBannerTitle = "✕ Verification Failed (Settings Not Locked)";
                    ConnectionBannerDetails = result.Message;
                    ConnectionBannerLevel = "ERROR";
                    HasConnectionBanner = true;

                    AddActivityLog($"[FAILED] Save & verify failed: {result.Message}");
                    MessageBox.Show($"Could not connect to SDVoE Control Server at {ServerIp}:{ServerPort}.\n\nDetails: {result.Message}", "Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ConnectionBannerTitle = "✕ Error Saving Settings";
                ConnectionBannerDetails = ex.Message;
                ConnectionBannerLevel = "ERROR";
                HasConnectionBanner = true;
                AddActivityLog($"[ERROR] Error saving settings: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void UnlockSettings()
        {
            IsSettingsLocked = false;
            ConnectionBannerTitle = "ℹ Server Settings Unlocked";
            ConnectionBannerDetails = "You can modify Control Server IP & Port. Click 'Save & Verify' when ready.";
            ConnectionBannerLevel = "INFO";
            AddActivityLog("Server settings unlocked for editing.");
        }

        public async Task QueryDecodersAsync()
        {
            IsBusy = true;
            BusyMessage = "Querying SDVoE decoders from network...";
            try
            {
                await QueryDecodersInternalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task QueryDecodersInternalAsync()
        {
            AddActivityLog($"Querying SDVoE decoders from {ServerIp}:{ServerPort}...");
            try
            {
                var queried = await _sdvoeService.QueryDecodersAsync(ServerIp, ServerPort, TimeoutMs);

                RunOnUi(() =>
                {
                    Decoders.Clear();
                    foreach (var item in queried)
                    {
                        // Restore selection if previously saved, or select all by default if no prior selection
                        if (_settings.SelectedDecoderMacs.Count == 0 || _settings.SelectedDecoderMacs.Contains(item.MacAddress))
                        {
                            item.IsSelected = true;
                        }

                        // Listen to IsSelected changes to update command CanExecute
                        item.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(DecoderItem.IsSelected))
                            {
                                OnPropertyChanged(nameof(SelectedDecodersCount));
                                OnPropertyChanged(nameof(CanStartTest));
                            }
                        };

                        Decoders.Add(item);
                    }

                    OnPropertyChanged(nameof(TotalDecodersCount));
                    OnPropertyChanged(nameof(SelectedDecodersCount));
                    OnPropertyChanged(nameof(CanStartTest));
                });

                AddActivityLog($"Discovery complete: Found {queried.Count} decoder(s) in SDVoE network.");
            }
            catch (Exception ex)
            {
                AddActivityLog($"[ERROR] Failed to query decoders: {ex.Message}");
            }
        }

        public async Task StartTestAsync()
        {
            var selected = Decoders.Where(d => d.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one Decoder (RX) to monitor.", "No Decoders Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save selected MACs to settings
            _settings.SelectedDecoderMacs = selected.Select(d => d.MacAddress).ToList();
            await _settingsService.SaveSettingsAsync(_settings);

            IsBusy = true;
            BusyMessage = "Initializing test session and CSV logging...";

            try
            {
                // Reset KPIs
                KpiMonitoredDecoders = selected.Count;
                KpiTotalSamples = 0;
                KpiPassingCount = 0;
                KpiIssuesCount = 0;
                KpiAnomalyCount = 0;
                KpiHealthPercentage = 100.0;
                KpiVideoReceivedCount = 0;
                KpiNoVideoCount = 0;
                KpiDisplaysActiveCount = 0;
                KpiDisplaysIssueCount = 0;
                SelectedTelemetryRow = null;
                ElapsedTimeText = "00:00:00";
                EncoderStatusText = "DISCOVERING / INITIALIZING";
                EncoderRasterText = "--";
                EncoderTempText = "--";
                EncoderOverallStatus = "UNKNOWN";

                // Initialize Live Telemetry Grid with selected decoders
                LiveTelemetry.Clear();
                foreach (var dec in selected)
                {
                    LiveTelemetry.Add(new DecoderTelemetrySample
                    {
                        DecoderName = dec.DeviceName,
                        MacAddress = dec.MacAddress,
                        IpAddress = dec.IpAddress,
                        OverallStatus = "INITIALIZING",
                        Notes = "Starting test cycle..."
                    });
                }

                string sessionDir = await _monitorEngine.StartAsync(_settings, selected);
                CurrentSessionDir = sessionDir;
                IsTestRunning = true;
                TestStatusText = "RUNNING - Active Monitoring";
                _uiTimer.Start();

                AddActivityLog($"[TEST STARTED] Monitoring {selected.Count} decoders. Logs directory: {sessionDir}");
            }
            catch (Exception ex)
            {
                AddActivityLog($"[ERROR] Failed to start test cycle: {ex.Message}");
                MessageBox.Show($"Failed to start test cycle: {ex.Message}", "Test Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task StopTestAsync()
        {
            IsBusy = true;
            BusyMessage = "Concluding test cycle and writing summary CSV...";

            try
            {
                _uiTimer.Stop();
                var summary = await _monitorEngine.StopAsync();
                IsTestRunning = false;
                TestStatusText = "STOPPED";

                if (summary != null)
                {
                    AddActivityLog($"[TEST COMPLETED] Run duration: {summary.Duration:hh\\:mm\\:ss}. Total samples: {summary.TotalSamples}, Pass: {summary.PassCount}, Fail: {summary.FailCount}.");
                }
            }
            catch (Exception ex)
            {
                AddActivityLog($"[ERROR] Error stopping test cycle: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnTelemetrySampleReceived(object? sender, DecoderTelemetrySample sample)
        {
            RunOnUi(() =>
            {
                // Update live telemetry row for this decoder
                var existing = LiveTelemetry.FirstOrDefault(t => string.Equals(t.MacAddress, sample.MacAddress, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    int index = LiveTelemetry.IndexOf(existing);
                    LiveTelemetry[index] = sample;
                }
                else
                {
                    LiveTelemetry.Add(sample);
                }

                if (SelectedTelemetryRow != null && string.Equals(SelectedTelemetryRow.MacAddress, sample.MacAddress, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedTelemetryRow = sample;
                }

                // Update KPI Counters
                KpiTotalSamples++;
                if (sample.OverallStatus == "PASS")
                {
                    KpiPassingCount++;
                }
                else
                {
                    KpiIssuesCount++;
                }

                if (KpiTotalSamples > 0)
                {
                    KpiHealthPercentage = Math.Round((double)KpiPassingCount / KpiTotalSamples * 100.0, 1);
                }

                // Recalculate Q1 and Q2 aggregate states across monitored decoders
                KpiVideoReceivedCount = LiveTelemetry.Count(t => t.VideoReceivedStatus == "RECEIVED" || t.VideoReceivedStatus == "RECEIVING");
                KpiNoVideoCount = LiveTelemetry.Count(t => t.VideoReceivedStatus != "RECEIVED" && t.VideoReceivedStatus != "RECEIVING");
                KpiDisplaysActiveCount = LiveTelemetry.Count(t => t.DisplayScreenStatus == "DISPLAYING_VIDEO" || t.DisplayScreenStatus == "DISPLAYING");
                KpiDisplaysIssueCount = LiveTelemetry.Count(t => t.DisplayScreenStatus != "DISPLAYING_VIDEO" && t.DisplayScreenStatus != "DISPLAYING");
            });
        }

        private void OnEncoderTelemetryReceived(object? sender, EncoderTelemetrySample sample)
        {
            RunOnUi(() =>
            {
                EncoderOverallStatus = sample.OverallStatus;
                EncoderMacText = sample.MacAddress;
                EncoderRasterText = sample.VideoRaster;
                EncoderTempText = sample.TemperatureC.HasValue ? $"{sample.TemperatureC.Value:F1}°C" : "--";
                EncoderStatusText = sample.HasVideo
                    ? $"ACTIVE ({sample.VideoRaster})"
                    : (sample.IsSourceStable ? "STABLE / NO VIDEO" : "UNSTABLE / REBOOTING");
            });
        }

        private void OnAnomalyDetected(object? sender, AnomalyEvent anomaly)
        {
            RunOnUi(() =>
            {
                KpiAnomalyCount++;
                string decInfo = !string.IsNullOrEmpty(anomaly.AffectedDecoderName) ? $" on {anomaly.AffectedDecoderName}" : "";
                AddActivityLog($"⚠️ [{anomaly.EventType}]{decInfo}: {anomaly.Description}");
            });
        }

        private void OnSessionCompleted(object? sender, TestSessionSummary summary)
        {
            RunOnUi(() =>
            {
                IsTestRunning = false;
                TestStatusText = "STOPPED";
                _uiTimer.Stop();
            });
        }

        private void SelectAllDecoders()
        {
            foreach (var d in Decoders)
            {
                d.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedDecodersCount));
            OnPropertyChanged(nameof(CanStartTest));
        }

        private void DeselectAllDecoders()
        {
            foreach (var d in Decoders)
            {
                d.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedDecodersCount));
            OnPropertyChanged(nameof(CanStartTest));
        }

        private bool FilterDecoders(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (item is DecoderItem d)
            {
                return (!string.IsNullOrEmpty(d.DeviceName) && d.DeviceName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(d.MacAddress) && d.MacAddress.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(d.IpAddress) && d.IpAddress.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return true;
        }

        private void OpenLogsFolder()
        {
            try
            {
                string targetDir = !string.IsNullOrEmpty(CurrentSessionDir) && Directory.Exists(CurrentSessionDir)
                    ? CurrentSessionDir
                    : Path.Combine(Directory.GetCurrentDirectory(), "logs");

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{targetDir}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AddActivityLog($"[ERROR] Could not open logs folder: {ex.Message}");
            }
        }

        private void AddActivityLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            RunOnUi(() =>
            {
                ActivityLogs.Insert(0, line);
                if (ActivityLogs.Count > 500)
                {
                    ActivityLogs.RemoveAt(ActivityLogs.Count - 1);
                }
            });
        }

        private void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }
    }
}
