# Encoder Monitoring & Intermittent Blackout Anomaly Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade AVAS Decoder Checker to **v1.0.2** by adding upstream TX encoder monitoring, a 2.0-second post-reboot recovery window state machine, and mid-stream intermittent blackout detection to isolate and benchmark sudden display dropouts.

**Architecture:** Extend SDVoE discovery and polling to capture upstream transmitter (TX) telemetry alongside RX decoders; pipe both streams through a new `AnomalyDetector` state machine that monitors (1) post-reboot recovery latency against a 2.0s window, and (2) mid-stream intermittent blackouts while the encoder maintains steady video output; update CSV loggers, ViewModels, and WPF UI with dedicated anomaly badges and incident reporting.

**Tech Stack:** C# .NET 8.0, WPF, MVVM, Newtonsoft.Json, VOIPS.dll SDK, xUnit.

**Spec:** [`docs/superpowers/specs/2026-09-26-encoder-monitoring-anomaly-detection-design.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/docs/superpowers/specs/2026-09-26-encoder-monitoring-anomaly-detection-design.md)

---

## Global Constraints

- **Version Number**: Strictly set to `1.0.2` in `AVASDecoderChecker.csproj`, assembly attributes, and UI footer.
- **Recovery Window**: Default to `2.0` seconds (`AppSettings.AnomalyWindowSeconds = 2.0`).
- **Intermittent Blackout Detection**: Explicitly detect mid-stream blackouts where encoder `HasVideo == true` but a decoder drops out, recording both transient recoveries and unrecovered persistent blackouts.
- **Backward Compatibility**: Non-breaking additions to existing CSV formats and sample models. If no encoders exist on the network, the app smoothly continues in decoder-only mode.
- **Test Coverage**: 100% test pass rate with new unit tests covering encoder evaluation, dual-link TX grouping, recovery timing, and intermittent blackout detection.

---

## Review Focus

1. **Mid-Stream Intermittent Blackouts**: A decoder was displaying video while encoder has video, but suddenly goes black/drops for a short time; ensure it captures the exact start, duration, whether it recovered before reboot, and logs an anomaly event.
2. **Dual-Link Transmitter Pairing**: Transmitters `74fe488b07bb` and `74fe488b07bc` must be evaluated together — composite `HasVideo` requires both chips to report `source_stable == true`.
3. **Recovery Window Threshold**: Decoders taking > 2.0s to display video after encoder returns must be marked `ANOMALY` (distinct from `FAIL` or `SOURCE_REBOOTING`).
4. **Transient Flaps & Fast Recovery**: If a blackout lasts < 2s and recovers before source reboots, log as `RECOVERED_BLACKOUT` with exact duration without false persistent alarms.
5. **No TX Device Graceful Fallback**: If SDVoE server has no transmitters discovered, `AnomalyDetector` stays dormant without throwing null reference exceptions.

---

## File Structure

### New Files
- `src/AVASDecoderChecker/Models/EncoderTelemetrySample.cs` — Model for upstream TX transmitter telemetry snapshots.
- `src/AVASDecoderChecker/Models/EncoderItem.cs` — Discovery item for transmitters on the SDVoE network.
- `src/AVASDecoderChecker/Models/AnomalyEvent.cs` — Incident record for post-reboot recovery timeouts and intermittent blackouts.
- `src/AVASDecoderChecker/Services/IAnomalyDetector.cs` — Interface for the anomaly state machine.
- `src/AVASDecoderChecker/Services/AnomalyDetector.cs` — Core state machine tracking post-reboot recovery windows and mid-stream intermittent blackouts.
- `tests/AVASDecoderChecker.Tests/EncoderEvaluationTests.cs` — Unit tests for TX evaluation and dual-link grouping.
- `tests/AVASDecoderChecker.Tests/AnomalyDetectorTests.cs` — Unit tests for 2s recovery window and intermittent blackouts.

### Modified Files
- `src/AVASDecoderChecker/AVASDecoderChecker.csproj` — Bump version to `1.0.2`.
- `src/AVASDecoderChecker/Models/AppSettings.cs` — Add `AnomalyWindowSeconds` (default: 2.0).
- `src/AVASDecoderChecker/Models/DecoderTelemetrySample.cs` — Add `SourceEncoderStatus`, `IsAnomaly`, `RecoveryTimeSec`, `BlackoutDurationSec`.
- `src/AVASDecoderChecker/Services/ISdvoeService.cs` & `SdvoeService.cs` — Add `QueryEncodersAsync` and `SampleEncoderTelemetryAsync`.
- `src/AVASDecoderChecker/Services/HealthEvaluator.cs` — Add `EvaluateEncoder` method and update decoder evaluation with encoder context.
- `src/AVASDecoderChecker/Services/IMonitorEngine.cs` & `MonitorEngine.cs` — Wire encoder polling and `AnomalyDetector.ProcessCycle`.
- `src/AVASDecoderChecker/Services/CsvLoggingService.cs` — Add `encoder_telemetry.csv`, `anomaly_events.csv`, and new master CSV columns.
- `src/AVASDecoderChecker/Converters/StatusConverters.cs` — Add `ANOMALY` (Purple `#A855F7`), `HAS_VIDEO`, `NO_VIDEO`.
- `src/AVASDecoderChecker/ViewModels/MainViewModel.cs` — Add encoder status properties, anomaly counter KPI, and activity logs.
- `src/AVASDecoderChecker/MainWindow.xaml` — Add TX Encoder pipeline card, Anomaly KPI card, update footer to `v1.0.2`.

---

## Tasks

### Task 1: Version Bump & Data Models (v1.0.2)

**Files:**
- Modify: `src/AVASDecoderChecker/AVASDecoderChecker.csproj`
- Create: `src/AVASDecoderChecker/Models/EncoderTelemetrySample.cs`
- Create: `src/AVASDecoderChecker/Models/EncoderItem.cs`
- Create: `src/AVASDecoderChecker/Models/AnomalyEvent.cs`
- Modify: `src/AVASDecoderChecker/Models/DecoderTelemetrySample.cs`
- Modify: `src/AVASDecoderChecker/Models/AppSettings.cs`

- [ ] **Step 1.1**: Update `AVASDecoderChecker.csproj` to Version `1.0.2`, AssemblyVersion `1.0.2.0`, FileVersion `1.0.2.0`.
- [ ] **Step 1.2**: Create `EncoderTelemetrySample.cs` with TX fields (`IsSourceStable`, `HasVideo`, `VideoRaster`, `PixelClockMhz`, `StreamState`, `MulticastAddress`, `LinkMode`, `LinkStatus`, `CompanionMac`, `Temperature`, `OverallStatus`, `Notes`).
- [ ] **Step 1.3**: Create `EncoderItem.cs` with `MacAddress`, `DeviceName`, `IpAddress`, `IsOnline`, `CompanionMac`.
- [ ] **Step 1.4**: Create `AnomalyEvent.cs` with `Timestamp`, `EventType` (`POST_REBOOT_TIMEOUT`, `MIDSTREAM_INTERMITTENT_DROPOUT`, `MIDSTREAM_RECOVERED`), `EncoderMac`, `AffectedDecoderMac`, `AffectedDecoderName`, `DurationSeconds`, `Description`.
- [ ] **Step 1.5**: Extend `DecoderTelemetrySample.cs` with `SourceEncoderStatus` ("HAS_VIDEO", "NO_VIDEO", "UNKNOWN"), `IsAnomaly` (bool), `RecoveryTimeSec` (double), and `BlackoutDurationSec` (double).
- [ ] **Step 1.6**: Add `AnomalyWindowSeconds` to `AppSettings.cs` defaulting to `2.0`.
- [ ] **Step 1.7**: Verify project compiles cleanly with `dotnet build`.

---

### Task 2: HealthEvaluator TX Evaluation & Dual-Link Logic

**Files:**
- Modify: `src/AVASDecoderChecker/Services/HealthEvaluator.cs`
- Create: `tests/AVASDecoderChecker.Tests/EncoderEvaluationTests.cs`

- [ ] **Step 2.1**: Write failing unit tests in `EncoderEvaluationTests.cs`:
  - `EvaluateEncoder_WithStableSource_ReturnsHasVideoTrue`
  - `EvaluateEncoder_WithUnlockedSource_ReturnsHasVideoFalse`
  - `EvaluateEncoder_DualLink_RequiresBothTransmittersActive`
- [ ] **Step 2.2**: Implement `HealthEvaluator.EvaluateEncoder(string rawJson, EncoderItem encoder)`:
  - Check `HDMI_DECODER` node (input from source PC): `source_stable`, `Video.width/height`, `video_details.pixel_clock`.
  - Check `streams` node for active multicast address and streaming state.
  - Check `MULTI_LINK_TRANSMITTER` for dual-link sync and companion MAC.
  - Extract chip temperature from `status.temperature`.
  - Determine `HasVideo = source_stable && pixel_clock > 0 && is_streaming`.
- [ ] **Step 2.3**: Update `HealthEvaluator.ComputeOverall` to recognize encoder state and support `ANOMALY` verdict.
- [ ] **Step 2.4**: Run `dotnet test --filter EncoderEvaluationTests` and ensure all tests pass.

---

### Task 3: AnomalyDetector State Machine & Intermittent Blackout Engine

**Files:**
- Create: `src/AVASDecoderChecker/Services/IAnomalyDetector.cs`
- Create: `src/AVASDecoderChecker/Services/AnomalyDetector.cs`
- Create: `tests/AVASDecoderChecker.Tests/AnomalyDetectorTests.cs`

- [ ] **Step 3.1**: Write failing unit tests in `AnomalyDetectorTests.cs`:
  - `ProcessCycle_EncoderAlive_DecoderRecoversUnder2s_NoAnomaly`
  - `ProcessCycle_EncoderAlive_DecoderFailsToRecoverAfter2s_FlagsAnomaly`
  - `ProcessCycle_EncoderActive_DecoderSuddenlyDropsOut_DetectsIntermittentBlackout`
  - `ProcessCycle_IntermittentBlackoutRecoversBeforeReboot_LogsRecoveredDuration`
  - `ProcessCycle_EncoderDrops_CancelsRecoveryWindowWithoutFalseAnomaly`
- [ ] **Step 3.2**: Implement `IAnomalyDetector` and `AnomalyDetector`:
  - Track `EncoderState` (previous `HasVideo`, `VideoAliveTimestamp`, `InRecoveryWindow`).
  - Track `DecoderBlackoutState` for each decoder (`LastKnownDisplaying`, `BlackoutStartTimestamp`, `IsCurrentlyBlackedOut`).
  - Configurable `AnomalyWindowSeconds` (default: 2.0s).
  - Fire `AnomalyDetected` event with `AnomalyEvent` payload.
- [ ] **Step 3.3**: Run `dotnet test --filter AnomalyDetectorTests` and ensure 100% pass.

---

### Task 4: SdvoeService & MonitorEngine Integration

**Files:**
- Modify: `src/AVASDecoderChecker/Services/ISdvoeService.cs`
- Modify: `src/AVASDecoderChecker/Services/SdvoeService.cs`
- Modify: `src/AVASDecoderChecker/Services/IMonitorEngine.cs`
- Modify: `src/AVASDecoderChecker/Services/MonitorEngine.cs`

- [ ] **Step 4.1**: Add `QueryEncodersAsync` and `SampleEncoderTelemetryAsync` to `ISdvoeService` and `SdvoeService`:
  - Query all devices with `identity.is_transmitter == true`.
  - Fetch transmitter node settings via `GET_ALL_SETTINGS` / REST fallback.
- [ ] **Step 4.2**: Update `MonitorEngine`:
  - Discover encoders on startup / auto-detect transmitters.
  - In each polling cycle:
    1. Sample all transmitters (`EncoderTelemetrySample`).
    2. Sample all selected receivers (`DecoderTelemetrySample`).
    3. Pass both sets to `AnomalyDetector.ProcessCycle(...)`.
    4. Raise `EncoderTelemetrySampleReceived` and `AnomalyDetected` events.
- [ ] **Step 4.3**: Run existing `MonitorEngineTests` and update mock engine as needed to guarantee zero regressions.

---

### Task 5: CSV Logging Extensions for Encoders & Anomalies

**Files:**
- Modify: `src/AVASDecoderChecker/Services/CsvLoggingService.cs`
- Modify: `tests/AVASDecoderChecker.Tests/CsvLoggingServiceTests.cs`

- [ ] **Step 5.1**: Add `encoder_telemetry.csv` generation per session:
  - Header: `Timestamp,EncoderName,MAC,IP,OverallStatus,SourceStable,HasVideo,VideoRaster,PixelClockMhz,StreamState,MulticastAddress,LinkMode,LinkStatus,Temperature,Notes`.
- [ ] **Step 5.2**: Add `anomaly_events.csv` generation per session:
  - Header: `Timestamp,EventType,ElapsedSeconds,EncoderMAC,AffectedDecoderName,AffectedDecoderMAC,Description`.
- [ ] **Step 5.3**: Extend `master_telemetry.csv` and per-decoder CSVs with:
  - `SourceEncoderStatus`, `IsAnomaly`, `RecoveryTimeSec`, `BlackoutDurationSec`.
- [ ] **Step 5.4**: Update `CsvLoggingServiceTests` to verify `encoder_telemetry.csv`, `anomaly_events.csv`, and new master CSV columns.
- [ ] **Step 5.5**: Run `dotnet test --filter CsvLoggingServiceTests` and verify all tests pass.

---

### Task 6: UI, Converters & ViewModel Integration (v1.0.2)

**Files:**
- Modify: `src/AVASDecoderChecker/Converters/StatusConverters.cs`
- Modify: `src/AVASDecoderChecker/ViewModels/MainViewModel.cs`
- Modify: `src/AVASDecoderChecker/MainWindow.xaml`

- [ ] **Step 6.1**: Update `StatusConverters.cs`:
  - Add `"ANOMALY"` -> Purple `#A855F7` / `#2E1065`.
  - Add `"HAS_VIDEO"`, `"VIDEO ACTIVE"` -> Green.
  - Add `"NO_VIDEO"`, `"REBOOTING"` -> Red/Amber.
- [ ] **Step 6.2**: Update `MainViewModel.cs`:
  - Handle `EncoderTelemetrySampleReceived` and update `EncoderStatusText`, `EncoderRasterText`, `EncoderTempText`.
  - Handle `AnomalyDetected` event: increment `KpiAnomalyCount` and push alert to `ActivityLogs`.
- [ ] **Step 6.3**: Update `MainWindow.xaml`:
  - Change footer to: `AVAS Decoder Checker v1.0.2 (.NET 8.0)`.
  - Add compact **TX Encoder Status Header Card** above the live telemetry grid.
  - Add **Anomalies KPI Card** next to Passing/Issues counters.
  - Add `Recovery (s)` and `Anomaly` badge support in the DataGrid.
- [ ] **Step 6.4**: Run `dotnet test` across all tests to verify 100% pass rate.

---

### Task 7: Release Build, Verification & Documentation

**Files:**
- Modify: `PROJECT_MEMORY.md`
- Modify: `README.md`

- [ ] **Step 7.1**: Run `dotnet build -c Release` and `dotnet test -c Release`.
- [ ] **Step 7.2**: Check `(Get-Item "src\AVASDecoderChecker\bin\Release\net8.0-windows\AVASDecoderChecker.exe").VersionInfo` to verify version `1.0.2.0`.
- [ ] **Step 7.3**: Package release zip `AVAS-Decoder-Checker-v1.0.2.zip` and update `AVAS-Decoder-Checker.zip`.
- [ ] **Step 7.4**: Update `PROJECT_MEMORY.md` and `README.md` with v1.0.2 details.
- [ ] **Step 7.5**: Submit final report and verification proof to the user.
