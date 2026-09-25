# PROJECT MEMORY: AVAS Decoder (RX) HDMI Stream Checker

> **Persistent Workspace Memory**  
> *Last Updated:* 2026-09-26  
> *Repository:* `AVAS-Decoder-Checker`  
> *Version:* `1.0.2`  
> *Workspace Root:* `c:\Users\POC-615\Documents\GitHub\AVAS-Decoder-Checker`

---

## 1. Project Overview & Goal
This project is an automated **Decoder (RX) HDMI Stream Checker** tailored for AVAS / SDVoE hardware environments, built as a modern C# .NET 8.0 WPF diagnostic desktop application.

### Operational Workflow:
1. **Target Parameters**:
   - SDVoE Control Server IP & Port (configured via UI or `config/settings.json`, default `127.0.0.1:8090`).
   - "Save & Verify": Tests connection to the SDVoE Control Server upon saving settings.
2. **Device Discovery & Selection**:
   - Discovers all RX decoders and upstream TX transmitters on the SDVoE network via `VOIPS.dll` and REST fallback.
   - Interactive table with multi-select checkboxes, search filter, and "Select All" / "Clear" buttons.
   - Remembers previously selected decoders in `config/settings.json`.
3. **Execution Commands**:
   - **START TEST**: Initiates background polling of transmitters and decoders, feeding telemetry to the Anomaly Detector state machine.
   - **STOP TEST**: Halts the checker process gracefully and generates summary reports.
4. **Upstream Transmitter (TX) Telemetry & Dual-Link Coupling (v1.0.2)**:
   - Polls upstream transmitters (`identity.is_transmitter == true`), capturing source lock (`HDMI_DECODER.source_stable`), pixel clock, raster, and chip temperature.
   - Dual-Link Transmitter Pairing: Accurately evaluates dual-link transmitters (e.g. `74fe488b07bb` / `74fe488b07bc`), requiring composite video stability across both transmission chips for 4K video feeds.
   - Dedicated Upstream TX Pipeline Status Bar in the UI header and `encoder_telemetry.csv` time-series log.
5. **Anomaly Detection State Machine (2.0s Recovery Window & Mid-Stream Blackouts)**:
   - **Post-Reboot Recovery Latency**: Default 2.0s recovery window (`AppSettings.AnomalyWindowSeconds = 2.0`). Flags `ANOMALY` (`POST_REBOOT_TIMEOUT`) if any decoder takes > 2.0s to display video after the source boots.
   - **Intermittent Mid-Stream Blackouts**: When encoder continuously outputs video (`HasVideo == true`), sudden dropouts on previously active displays are tracked. The engine measures exact blackout duration, records whether the display recovered before the next reboot (`MIDSTREAM_RECOVERED`) or stayed blacked out (`MIDSTREAM_INTERMITTENT_DROPOUT`), and logs incident details to `anomaly_events.csv`.
6. **HDMI Stream Health Assessment & Root-Cause Attribution**:
   - **Question 1 (RX Inbound)**: Is video being received from the encoder? (`streaming`, `stable`, `SourceEncoderMac`, `MultiLinkStatus`, `PixelClockMhz`).
   - **Question 2 (RX Outbound)**: Is the connected display actually displaying video? (`HDMI_MONITOR.connected`, `edid`, `tmds_clock`, `hdcp_blocked`).
   - **Fault Attribution Isolation Categories**:
     - `NONE`: Fully healthy operation.
     - `SOURCE_REBOOTING`: Video source PC rebooting on automated cycle (source_stable=false, ~44.5s outage window).
     - `POST_REBOOT_RECOVERY_TIMEOUT`: Decoder failed to restore display output within 2.0s after source rebooted.
     - `DECODER_PLL_DESYNC`: Decoder AVP2000 PLL multiplier desynchronization glitch.
     - `DECODER_DUAL_DESYNC`: Multi-link companion receiver lost synchronization.
     - `DECODER_STREAM_LOSS`: Decoder dropped or unsubscribed from multicast group.
     - `DISPLAY_HPD_DOWN`: Monitor turned off, in deep sleep, or HDMI cable disconnected.
     - `DISPLAY_EDID_CORRUPT`: Monitor connected via HPD but unresponsive on DDC channel.
     - `DISPLAY_HDCP_BLOCKED`: Sink unauthenticated for HDCP protected media.
     - `SERVER_TIMEOUT`: Control server communication failure.
7. **Multi-Level CSV Logging**:
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
| `HasVideo` (TX) | `HDMI_DECODER.source_stable` & `pixel_clock > 0` | `true` across paired chips | Composite transmitter video stability |
| `VideoReceivedStatus` | `subscriptions[HDMI].status.state` & `HDMI_ENCODER.source_stable` | `"RECEIVED"` | Active genlocked stream from encoder |
| `MultiLinkStatus` | `nodes[MULTI_LINK_RECEIVER].status.state` | `"SYNCHRONIZED"` | Dual-link companion decoders aligned |
| `DisplayScreenStatus` | `HDMI_MONITOR.connected` & `status.edid` & TMDS clock | `"DISPLAYING_VIDEO"` | Monitor authenticated and actively displaying |
| `IsHpdConnected` | `HDMI_MONITOR_NODE_STATUS.connected` | `true` | Physical HDMI 5V / HPD pin asserted |
| `TemperatureC` | `DEVICE_OBJECT.status.temperature` | `< 68°C` | Hardware thermal condition within envelope |
| `FaultAttribution` | `HealthEvaluator.ComputeOverall` | Explicit enum | Root cause attribution (Decoder vs Display vs Source) |
| `IsAnomaly` | `AnomalyDetector.ProcessCycle` | `false` | Recovery latency <= 2.0s and no mid-stream blackouts |

---

## 3. Configuration & Parameters State

### SDVoE Control Server
- **Server IP**: Configurable in UI (default: `127.0.0.1`)
- **Server Port**: Configurable in UI (default: `8090` / `80`)
- **SDK / Protocol**: `VOIPS.dll` (.NET Standard 2.0 / .NET 8.0) + HTTP REST fallback
- **Config Storage**: `config/settings.json`

### Anomaly Detection Parameters
- **Recovery Window**: Strictly `2.0` seconds (`AppSettings.AnomalyWindowSeconds = 2.0`).
- **Blackout Threshold**: Instantaneous capture on `WasDisplaying -> !DISPLAYING_VIDEO` while TX `HasVideo == true`.

### Target Devices (Decoders / RX & Encoders / TX)
- **Selection**: Interactive UI multi-select DataGrid with search filter and Select All / Deselect All.
- **Persistence**: Selected MAC addresses remembered in `config/settings.json`.

---

## 4. Architectural Principles & Implementation State
- **Engine**: C# .NET 8.0 (`net8.0-windows`) WPF MVVM Application.
- **Version**: **1.0.2**
- **Modules**:
  - `SettingsService`: Manages loading and persisting `config/settings.json`.
  - `SdvoeService`: Integration with `VOIPS.dll` SDK, encoder & decoder querying, and telemetry sampling.
  - `HealthEvaluator`: Comprehensive rule engine for health evaluation (`PASS`, `WARN`, `FAIL`, `ANOMALY`), transmitter evaluation, and root-cause fault isolation.
  - `AnomalyDetector`: Real-time state machine tracking 2.0s post-reboot recovery windows and mid-stream intermittent blackouts.
  - `CsvLoggingService`: Manages isolated run folders, `master_telemetry.csv`, `encoder_telemetry.csv`, `anomaly_events.csv`, per-decoder CSVs, and `summary_report.csv`.
  - `MonitorEngine`: Periodic background polling engine orchestrating encoder and decoder polling cycles and anomaly detection.
  - `MainViewModel`: Reactive state coordinator with live KPI counters (including Anomalies count), TX pipeline banner, status badges, and activity log.
  - `MainWindow`: Modern dark-slate dashboard UI with DataGrid Fault Attribution column, Recovery & Anomaly column, Upstream TX Pipeline Card, and Hardware Diagnostic Inspector.
- **Automated Tests**: 41 xUnit tests in `tests/AVASDecoderChecker.Tests` covering encoder evaluation, dual-link grouping, recovery timing, intermittent blackout detection, CSV generation, polling engine, and ViewModel. All 100% passing in Debug and Release.

---

## 5. Action Items & Current Status
- [x] Upgrade codebase and assembly version to **1.0.2**.
- [x] Implement upstream transmitter monitoring and dual-link pairing in `HealthEvaluator`.
- [x] Implement `AnomalyDetector` state machine with strict 2.0s recovery window and mid-stream intermittent blackout detection.
- [x] Extend `CsvLoggingService` with `encoder_telemetry.csv`, `anomaly_events.csv`, and new master CSV columns.
- [x] Add Upstream TX Pipeline Card and Anomalies KPI badge to WPF UI.
- [x] Add `RECOVERY & ANOMALY` DataGrid column in `MainWindow.xaml`.
- [x] Pass 100% unit tests (41 / 41 passing).
- [x] Verify Release build (`AVASDecoderChecker.exe` Version 1.0.2.0).
- [x] Package `AVAS-Decoder-Checker-v1.0.2.zip` and update `AVAS-Decoder-Checker.zip`.

