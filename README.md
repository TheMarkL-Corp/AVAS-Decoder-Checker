# AVAS SDVoE Decoder (RX) HDMI Stream Checker (v1.0.3)

> **GitHub Repository:** [TheMarkL-Corp/AVAS-Decoder-Checker](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker)  
> **Latest Release:** [Release v1.0.3](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker/releases/tag/v1.0.3)  
> **Direct Download:** [AVAS-Decoder-Checker-v1.0.3.zip](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker/releases/download/v1.0.3/AVAS-Decoder-Checker-v1.0.3.zip)  
> **Comprehensive Technical Spec:** [`docs/SOFTWARE_SPECIFICATION.md`](docs/SOFTWARE_SPECIFICATION.md)  
> **Architecture Reference:** [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)  

A modern, high-performance C# .NET 8.0 WPF diagnostic dashboard and health monitor for AVAS / SDVoE hardware environments. It connects to the SDVoE Control Server, discovers all RX decoders and upstream TX transmitters, tracks real-time HDMI streaming and display sink conditions, isolates black screen root causes between the **Decoder**, the **Display**, and the **Source PC**, and provides automated **DHCP Fault Detection**, **Abrupt Power Loss vs. Network Disconnect Discrimination**, and **Anomaly Detection** with a strict **2.0-second post-reboot recovery window**.

---

## Key Features in v1.0.3

1. **Decoder DHCP Fault Detection**:
   - Inspects `NETWORK_INTERFACE` configuration and live status (`mode`, `ip.address`, `mask`, `gateway`).
   - Flags decoders configured for DHCP that failed IP acquisition and fell back to APIPA Auto-IP (`169.254.x.x`) or unassigned `0.0.0.0` as `DECODER_DHCP_FAULT`.
   - Distinct Red (`#EF4444`) badge in the UI and telemetry grid with live IP Mode and Subnet details in the Hardware Diagnostic Inspector.

2. **Abrupt Power Loss vs. Network Disconnect Discrimination**:
   - Differentiates sudden physical power failure (unplugged DC barrel jack / power rail collapse) from a dropped Ethernet link.
   - Correlates Control Server reachability, direct LAN TCP socket probing (200ms timeout to decoder MCU IP), and HDMI Hot-Plug Detect (Pin 19) state:
     - `DECODER_POWER_LOSS`: Decoder unreachable on Control Server AND direct LAN ping fails AND HDMI 5V / HPD pin 19 drops low (display goes into standby).
     - `NETWORK_LINK_DOWN`: Decoder unreachable on Control Server AND LAN ping fails BUT display HPD pin 19 remains high (decoder remains powered on).
     - `SERVER_TIMEOUT`: Control Server times out, but direct LAN socket probe succeeds (decoder is alive; server software is unresponsive).

3. **Upstream Transmitter (TX / Encoder) Telemetry Monitoring**:
   - Discovers and polls active upstream transmitters on the network (`identity.is_transmitter == true`).
   - Tracks transmitter video lock (`HDMI_DECODER.source_stable`), pixel clock (`video_details.pixel_clock`), multicast streaming state, and internal chip temperature.
   - Dual-Link Transmitter Pairing: Accurately evaluates dual-link transmitters (e.g. `74fe488b07bb` / `74fe488b07bc`), requiring composite video stability across both transmission chips for 4K video feeds.
   - Dedicated Upstream TX Pipeline Status Bar in the UI header and `encoder_telemetry.csv` time-series log.

4. **Automated Anomaly Detection State Machine (2.0s Recovery Window)**:
   - **Post-Reboot Recovery Latency**: When the source PC boots up and encoder video transitions `HasVideo: false -> true`, the engine monitors each decoder. If any decoder takes > 2.0s to display video, it is flagged as an `ANOMALY` (`POST_REBOOT_TIMEOUT`).
   - **Intermittent Mid-Stream Blackouts**: When the encoder continuously outputs video (`HasVideo == true`), sudden dropouts on previously active displays are tracked. The engine measures exact blackout duration, records whether the display recovered before the next reboot (`MIDSTREAM_RECOVERED`) or stayed blacked out (`MIDSTREAM_INTERMITTENT_DROPOUT`), and logs incident details to `anomaly_events.csv`.
   - Distinct Purple (`#A855F7`) badges for anomalies across the UI and telemetry DataGrid.

5. **Root-Cause Fault Attribution & Black Screen Diagnostics**:
   - Definitively answers whether intermittent black screens are caused by the **Decoder (RX)**, the **Display (Sink)**, or the **Source PC**.
   - **Fault Attribution Categories**:
     - `NONE`: Normal, healthy operation.
     - `DECODER_DHCP_FAULT`: Decoder failed DHCP lease and reverted to APIPA or 0.0.0.0.
     - `DECODER_POWER_LOSS`: Decoder abruptly lost DC power (LAN dead + HPD pin 19 dropped low).
     - `NETWORK_LINK_DOWN`: Decoder Ethernet disconnected while powered on (LAN dead + HPD retained).
     - `SOURCE_REBOOTING`: Source video PC power-cycling (~44.5s normal boot outage).
     - `POST_REBOOT_RECOVERY_TIMEOUT`: Decoder failed to restore display output within 2.0s after source rebooted.
     - `DECODER_PLL_DESYNC`: Decoder AVP2000 PLL multiplier desynchronization (~83.8% anomalous pixel clock: 248.8 MHz / 497.7 MHz).
     - `DECODER_DUAL_DESYNC`: Multi-link companion receiver (`MULTI_LINK_RECEIVER`) lost synchronization.
     - `DECODER_STREAM_LOSS`: Decoder dropped or unsubscribed from multicast group.
     - `DISPLAY_HPD_DOWN`: Physical HDMI cable unplugged or display entered deep sleep (Hot-Plug Detect pin 19 low).
     - `DISPLAY_EDID_CORRUPT`: Display asserted HPD but failed VESA EDID handshake over DDC channel.
     - `DISPLAY_HDCP_BLOCKED`: Content blanked by HDCP encryption handshake failure.
     - `SERVER_TIMEOUT`: Control server communication failure.

4. **Hardware Diagnostic Inspector & Extended Telemetry**:
   - **Chip Temperature Monitoring**: Live internal thermal tracking (`status.temperature`) for decoders and encoders with >68°C warnings.
   - **Multi-Link Status**: Tracks Dual-Link (`DUAL`/`SINGLE`) synchronization across paired transmitters/receivers.
   - **10G Network Port Telemetry**: Monitors link speed and port active status.
   - **Transmitter MAC Resolution**: Discovers upstream transmitter MAC addresses (`SourceEncoderMac`) from multicast stream routes (`224.1.1.x`).
   - **VESA EDID Hex Decoder**: Decodes manufacturer name, monitor model (e.g. `VP3268-4K`), serial number, and preferred timings directly from I2C EEPROM.

5. **Multi-Level CSV Logging & Run Isolation**:
   - Each test cycle creates an isolated directory: `logs/Test_YYYYMMDD_HHmmss/`.
   - **Master Telemetry CSV** (`master_telemetry.csv`): Chronological log of all polling samples with full fault attribution, source encoder status, and anomaly timing columns.
   - **Encoder Telemetry CSV** (`encoder_telemetry.csv`): Continuous telemetry log of upstream transmitter health, raster, and temperatures.
   - **Anomaly Events CSV** (`anomaly_events.csv`): Incident ledger logging exact timestamps, event types, blackout durations, and affected decoders.
   - **Per-Decoder CSVs** (`decoders/<DecoderName>_<MAC>.csv`): Dedicated time-series telemetry log for each monitored decoder.
   - **Run Summary Report** (`summary_report.csv`): Comprehensive session summary detailing duration, total samples, pass/fail counts, and per-decoder streaming/display uptime percentages.

---

## Quick Start

### Prerequisites
- Windows 10/11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Installed)

### Launching the Application
You can run the application directly from the repository using PowerShell or CMD:

```powershell
# Using the PowerShell launcher:
.\scripts\run-app.ps1

# Or with dotnet CLI:
dotnet run --project src\AVASDecoderChecker -c Release
```

Or double-click `scripts\run-app.bat` in Windows File Explorer.

### Running Automated Tests
```powershell
dotnet test AVASDecoderChecker.sln
```

---

## Operational Workflow

```
[ Step 1: Server Settings ]
       │
       ▼
   Enter Control Server IP & Port
   Click "Save & Verify" (Validates connectivity & persists settings)
       │
       ▼
[ Step 2: Decoder Discovery ]
       │
       ▼
   Network decoders automatically populate in the table
   Check the Decoders (RX) you wish to monitor
       │
       ▼
[ Step 3: Live Test Monitoring ]
       │
       ▼
   Click "▶ START TEST"
   - UI status updates to RUNNING
   - KPI counters and Live Telemetry grid update in real-time
   - Dedicated folder logs/Test_YYYYMMDD_HHmmss/ created with CSV logs
       │
       ▼
   Click "⏹ STOP TEST"
   - Concludes test session
   - Generates summary_report.csv
   - Click "📁 Open Logs Folder" to view CSV logs
```

---

## Repository Structure

```
AVAS-Decoder-Checker/
├── AVASDecoderChecker.sln                 # .NET 8.0 Solution
├── src/
│   └── AVASDecoderChecker/               # WPF Desktop Application (.NET 8)
│       ├── Models/                       # AppSettings, DecoderItem, TelemetrySample, SessionSummary
│       ├── Services/                     # SettingsService, SdvoeService (VOIPS.dll), CsvLoggingService, MonitorEngine, HealthEvaluator
│       ├── ViewModels/                   # MainViewModel, ViewModelBase, RelayCommand
│       ├── Converters/                   # Status-to-color & boolean WPF converters
│       ├── Themes/                       # ModernTheme.xaml (Dark Slate Dashboard)
│       ├── MainWindow.xaml / .cs         # Modern Dashboard UI View
│       └── App.xaml / .cs                # Application Entry Point
├── tests/
│   └── AVASDecoderChecker.Tests/         # Automated Unit & Integration Tests (xUnit)
│       ├── TelemetryEvaluationTests.cs  # Health matrix & condition verification tests
│       ├── DhcpAndPowerFaultTests.cs     # DHCP lease failure & abrupt power loss diagnostics tests
│       ├── SettingsServiceTests.cs       # Settings persistence tests
│       ├── CsvLoggingServiceTests.cs     # CSV file formatting & directory isolation tests
│       ├── MonitorEngineTests.cs         # Background polling & lifecycle tests
│       └── MainViewModelTests.cs         # MVVM commands & state tests
├── config/
│   ├── config.default.json               # Default configuration reference
│   └── settings.json                     # Active application settings
├── scripts/
│   ├── run-app.ps1                       # PowerShell launcher
│   └── run-app.bat                       # Windows batch launcher
├── logs/                                 # Dedicated timestamped test directories
└── AVoIP SDK and SDVoE Reference/        # Official SDVoE API documentation & VOIPS.dll SDK
```
