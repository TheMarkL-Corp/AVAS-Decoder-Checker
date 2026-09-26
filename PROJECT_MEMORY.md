# PROJECT MEMORY: AVAS Decoder (RX) HDMI Stream Checker

> **Persistent Workspace Memory**  
> *Last Updated:* 2026-09-26  
> *Repository:* [TheMarkL-Corp/AVAS-Decoder-Checker](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker)  
> *Remote Status:* Public GitHub Repository, synchronized on `origin/main`  
> *Latest Release:* [`v1.0.3`](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker/releases/tag/v1.0.3)  
> *Workspace Root:* `c:\Users\POC-615\Documents\GitHub\AVAS-Decoder-Checker`  
> *Software Specification:* [`docs/SOFTWARE_SPECIFICATION.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/docs/SOFTWARE_SPECIFICATION.md)  

---

## 1. Project Overview & Mission
This project is an automated **Decoder (RX) HDMI Stream Checker** tailored for AVAS / SDVoE hardware environments (Aquantia / Semtech BlueRiver AVP2000 chipsets), engineered as a modern, high-performance C# .NET 8.0 WPF diagnostic desktop application.

### Key Operational Workflow:
1. **Target Parameters & Connectivity**:
   - SDVoE Control Server IP & Port (configured via UI or `config/settings.json`, default `127.0.0.1:8090`).
   - "Save & Verify": Probes TCP port (8090 vs 80 auto-negotiation), tests `VOIPS.dll` Discovery API, displays latency (ms), and persists settings.
2. **Device Discovery & Selection**:
   - Concurrently discovers all RX decoders and upstream TX transmitters on the SDVoE network via `VOIPS.dll` and REST fallback.
   - Interactive table with multi-select checkboxes, real-time search filter, and "Select All" / "Clear" buttons.
   - Remembers selected decoders in `config/settings.json`.
3. **Execution Commands**:
   - **START TEST**: Initiates periodic background polling of upstream transmitters and selected decoders, feeding telemetry to the Anomaly Detector state machine and writing isolated CSV logs.
   - **STOP TEST**: Concludes the test run gracefully, computes session statistics, and generates `summary_report.csv`.
4. **Upstream Transmitter (TX) Telemetry & Dual-Link Coupling**:
   - Discovers and polls upstream transmitters (`identity.is_transmitter == true`), capturing source lock (`HDMI_DECODER.source_stable`), pixel clock, raster, stream status, and internal temperature.
   - **Dual-Link Pairing**: Dynamically correlates companion transmitters (e.g. `74fe488b07bb` / `74fe488b07bc`), requiring composite video stability across both transmission chips for 4K video feeds.
   - Dedicated Upstream TX Pipeline Status Card in the UI header and `encoder_telemetry.csv` time-series log.
5. **Anomaly Detection State Machine (2.0s Recovery Window & Mid-Stream Blackouts)**:
   - **Post-Reboot Recovery Latency**: Default 2.0s recovery window (`AppSettings.AnomalyWindowSeconds = 2.0`). When source PC boots and encoder video transitions `false -> true`, flags `ANOMALY` (`POST_REBOOT_TIMEOUT`) if any decoder takes > 2.0s to display video.
   - **Intermittent Mid-Stream Blackouts**: When encoder continuously outputs video (`HasVideo == true`), sudden dropouts on previously active displays are tracked. The engine measures exact blackout duration, records whether the display recovered before the next reboot (`MIDSTREAM_RECOVERED`) or stayed blacked out (`MIDSTREAM_INTERMITTENT_DROPOUT`), and logs incident details to `anomaly_events.csv`.
   - **Reboot Immunity**: Normal source reboot cycles (`HasVideo: true -> false`) cleanly clear blackout states so routine source reboot cycles are never falsely flagged as blackouts.
6. **Definitive Root-Cause Fault Attribution**:
   - **Question 1 (Inbound Video)**: Is video arriving from the encoder? (`RECEIVED`, `NO_STREAM`, `UNSUBSCRIBED`).
   - **Question 2 (Outbound Display)**: Is the connected display rendering video? (`DISPLAYING_VIDEO`, `NO_DISPLAY`, `HANDSHAKE_FAILED`, `BLACK_SCREEN`, `WAITING_FOR_SOURCE`).
   - **Fault Attribution Categories**:
     - `NONE`: Normal, fully healthy operation.
     - `SOURCE_REBOOTING`: Video source PC rebooting on automated cycle (~44.5s normal boot outage).
     - `POST_REBOOT_RECOVERY_TIMEOUT`: Decoder failed to restore display output within 2.0s after source rebooted.
     - `DECODER_PLL_DESYNC`: Decoder AVP2000 PLL multiplier desynchronization glitch (~83.8% anomalous pixel clock: 248.8 MHz / 497.7 MHz).
     - `DECODER_DUAL_DESYNC`: Multi-link companion receiver lost synchronization.
     - `DECODER_STREAM_LOSS`: Decoder dropped or unsubscribed from multicast group.
     - `DECODER_DHCP_FAULT`: Decoder failed to acquire a valid DHCP IP lease (APIPA fallback `169.254.x.x` or unassigned `0.0.0.0`).
     - `DECODER_POWER_LOSS`: Decoder experienced an abrupt, sudden power loss (device unreachable on LAN + display HPD pin collapses to 0V).
     - `NETWORK_LINK_DOWN`: Decoder is still powered on, but network connection was lost (device unreachable on LAN, but display HPD 5V remains active).
     - `DISPLAY_HPD_DOWN`: Monitor turned off, in deep sleep, or HDMI cable disconnected (HPD pin 19 low).
     - `DISPLAY_EDID_CORRUPT`: Monitor connected via HPD but unresponsive or corrupted on DDC channel.
     - `DISPLAY_HDCP_BLOCKED`: Sink unauthenticated for HDCP protected media.
     - `SERVER_TIMEOUT`: Direct LAN socket probe to decoder IP succeeds, but SDVoE Control Server software/port timed out.
7. **Multi-Level CSV Logging & Run Isolation**:
   - Isolated directory per test cycle: `logs/Test_YYYYMMDD_HHmmss/`.
   - `master_telemetry.csv`: Chronological samples across all selected decoders with full fault attribution, source encoder status, and anomaly timing columns.
   - `encoder_telemetry.csv`: Continuous telemetry log of upstream transmitter health, raster, and temperatures.
   - `anomaly_events.csv`: Incident ledger logging exact timestamps, event types, blackout durations, and affected decoders.
   - `decoders/<DecoderName>_<MAC>.csv`: Individual per-decoder telemetry CSVs.
   - `summary_report.csv`: Complete run statistics, duration, pass/fail counts, and uptime percentages.

---

## 2. Health & Verification Metrics Breakdown

| Metric / Path | Target Object in SDVoE API / VOIPS SDK | Target Condition | Diagnostic Meaning |
|---|---|---|---|
| `HasVideo` (TX) | `HDMI_DECODER.source_stable` & `pixel_clock > 0` & `isStreaming` | `true` across paired chips | Composite transmitter video stability |
| `VideoReceivedStatus` | `subscriptions[HDMI].status.state` & `HDMI_ENCODER.source_stable` | `"RECEIVED"` | Active genlocked stream from encoder |
| `MultiLinkStatus` | `nodes[MULTI_LINK_RECEIVER].status.state` | `"SYNCHRONIZED"` | Dual-link companion decoders aligned |
| `DisplayScreenStatus` | `HDMI_MONITOR.connected` & `status.edid` & TMDS clock | `"DISPLAYING_VIDEO"` | Monitor authenticated and actively displaying |
| `IsHpdConnected` | `HDMI_MONITOR_NODE_STATUS.connected` | `true` | Physical HDMI 5V / HPD pin asserted |
| `TemperatureC` | `DEVICE_OBJECT.status.temperature` | `< 68°C` | Hardware thermal condition within envelope |
| `FaultAttribution` | `HealthEvaluator.ComputeOverall` | Explicit enum | Root cause attribution (Decoder vs Display vs Source vs Server) |
| `IsAnomaly` | `AnomalyDetector.ProcessCycle` | `false` | Recovery latency <= 2.0s and no mid-stream blackouts |

---

## 3. Configuration & Parameters State

### SDVoE Control Server
- **Server IP**: Configurable in UI (default: `127.0.0.1`)
- **Server Port**: Configurable in UI (default: `8090` / `80`)
- **SDK / Protocol**: `VOIPS.dll` (.NET Standard 2.0 / .NET 8.0) + HTTP REST fallback
- **Config Storage**: `config/settings.json` (falls back to `config/config.default.json`)

### Anomaly Detection Parameters
- **Recovery Window**: Strictly `2.0` seconds (`AppSettings.AnomalyWindowSeconds = 2.0`).
- **Blackout Threshold**: Instantaneous capture on `WasDisplaying -> !DISPLAYING_VIDEO` while TX `HasVideo == true`.

### Target Devices (Decoders / RX & Encoders / TX)
- **Selection**: Interactive UI multi-select DataGrid with search filter and Select All / Deselect All.
- **Persistence**: Selected MAC addresses remembered in `config/settings.json`.

---

## 4. Codebase Architecture & File Tree

```
AVAS-Decoder-Checker/
├── AVASDecoderChecker.sln                 # .NET 8.0 Solution
├── PROJECT_MEMORY.md                      # Persistent workspace memory (this document)
├── README.md                              # Public documentation & user guide
├── config/
│   ├── config.default.json               # Default configuration fallback
│   └── settings.json                     # Active runtime settings
├── docs/
│   ├── SOFTWARE_SPECIFICATION.md         # Comprehensive software specification (v1.0.3)
│   ├── ARCHITECTURE.md                   # High-level architecture summary (v1.0.3)
│   ├── SDVOE_REFERENCE.md                # SDVoE API / VOIPS reference
│   └── superpowers/                      # Superpowers specs & execution plans
├── src/AVASDecoderChecker/
│   ├── App.xaml / App.xaml.cs            # WPF Application bootstrap
│   ├── AssemblyInfo.cs                   # Assembly version info (1.0.3.0)
│   ├── AVASDecoderChecker.csproj         # Project configuration (net8.0-windows)
│   ├── MainWindow.xaml / .cs             # Modern Dark Slate Dashboard UI
│   ├── Converters/
│   │   └── StatusConverters.cs           # Status-to-brush, badge, and visibility converters
│   ├── Models/
│   │   ├── AnomalyEvent.cs               # Anomaly incident record and event types
│   │   ├── AppSettings.cs                # JSON settings model
│   │   ├── DecoderItem.cs                # Observable decoder UI inventory item
│   │   ├── DecoderTelemetrySample.cs     # RX telemetry snapshot model
│   │   ├── EncoderItem.cs                # Observable encoder UI inventory item
│   │   ├── EncoderTelemetrySample.cs     # TX telemetry snapshot model
│   │   └── TestSessionSummary.cs         # Post-run test session metrics
│   ├── Services/
│   │   ├── AnomalyDetector.cs            # Real-time state machine for 2.0s recovery & blackouts
│   │   ├── CsvLoggingService.cs          # Thread-safe multi-tier CSV writer
│   │   ├── EdidParser.cs                 # VESA 128-byte hex EDID decoding engine
│   │   ├── HealthEvaluator.cs            # Dual-question rules & root-cause fault isolator
│   │   ├── IAnomalyDetector.cs           # Interface for anomaly detection
│   │   ├── ILoggingService.cs            # Interface for CSV logging
│   │   ├── IMonitorEngine.cs             # Interface for background orchestrator
│   │   ├── ISdvoeService.cs              # Interface for SDVoE communication
│   │   ├── ISettingsService.cs           # Interface for settings persistence
│   │   ├── MonitorEngine.cs              # Periodic background polling orchestrator
│   │   ├── SdvoeService.cs               # VOIPS.dll interop, port probing & REST fallback
│   │   └── SettingsService.cs            # Settings file load/save provider
│   ├── Themes/
│   │   └── ModernTheme.xaml              # Dark Slate UI design system & controls
│   └── ViewModels/
│       ├── MainViewModel.cs              # Reactive UI coordinator & data bindings
│       ├── RelayCommand.cs               # ICommand implementation
│       └── ViewModelBase.cs              # INotifyPropertyChanged base class
├── tests/AVASDecoderChecker.Tests/
│   ├── AnomalyDetectorTests.cs           # 2.0s window & midstream blackout tests
│   ├── AVASDecoderChecker.Tests.csproj   # Test project configuration (xUnit)
│   ├── CsvLoggingServiceTests.cs         # CSV writing & summary report tests
│   ├── DumpLiveDevicePayloadTests.cs     # Live hardware payload capture fixture
│   ├── EdidParserTests.cs                # VESA EDID parsing & checksum tests
│   ├── EncoderEvaluationTests.cs         # TX evaluation & dual-link coupling tests
│   ├── golden_RX_DECODER_*.json          # Real-world device JSON test fixtures
│   ├── HealthEvaluatorOverhaulTests.cs   # Fault attribution & isolation matrix tests
│   ├── InspectLiveJsonTests.cs           # JSON schema inspection fixture
│   ├── MainViewModelTests.cs             # UI state & command execution tests
│   ├── MonitorEngineTests.cs             # Polling lifecycle & cancellation tests
│   ├── SdvoeLiveIntegrationTests.cs      # Live hardware integration test suite
│   ├── SettingsServiceTests.cs           # Settings persistence tests
│   └── TelemetryEvaluationTests.cs       # RX telemetry & Question 1/2 tests
├── scripts/
│   ├── run-app.ps1                       # PowerShell launcher
│   └── run-app.bat                       # Windows batch launcher
├── logs/                                 # Dedicated timestamped run folders
└── AVoIP SDK and SDVoE Reference/        # Upstream API documentation & VOIPS.dll SDK
```

---

## 5. Verification & Testing Status

- **Automated Tests**: **50 / 50 passing** (`dotnet test` passed with 0 failures, 0 skipped).
- **Build Configurations**: Builds cleanly in both `Debug` and `Release` configurations.
- **Assembly Version**: `1.0.3.0` (`src/AVASDecoderChecker/AssemblyInfo.cs`).
- **Git Remote & Releases**:
  - Remote: `https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker`
  - Latest Release Tag: `v1.0.3`
  - Release Packages Attached:
    - `AVAS-Decoder-Checker-v1.0.3.zip` (666,327 bytes)
    - `AVAS-Decoder-Checker.zip` (666,327 bytes)
- **Documentation**:
  - Software Specification: [`docs/SOFTWARE_SPECIFICATION.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/docs/SOFTWARE_SPECIFICATION.md)
  - User Guide & Overview: [`README.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/README.md)
  - Architecture Summary: [`docs/ARCHITECTURE.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/docs/ARCHITECTURE.md)
