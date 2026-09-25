# Design Spec: Encoder Monitoring & Anomaly Detection

> **Date:** 2026-09-26  
> **Status:** Draft  
> **Scope:** Add TX (encoder) monitoring to existing test flow, correlate encoder/decoder state, and detect anomalies where encoder has video but decoder doesn't.

---

## 1. Problem Statement

The AVAS Decoder Checker currently monitors only RX (decoder) devices. When the video source connected to the encoder is rebooted every 60–80 seconds as part of a test cycle, the app sees recurring PASS→FAIL transitions but cannot distinguish between:

- **Expected failure:** Encoder has no video (source rebooting) → decoders have no video. This is normal.
- **Anomaly:** Encoder has video (source is back) → but one or more decoders still don't show video. This indicates a real problem.

Without encoder state, every "decoder has no video" event looks the same. The user needs to identify cases where the encoder is alive but the decoder/display pipeline fails to recover.

---

## 2. Goals

1. **Auto-discover and poll TX (encoder) devices** from the SDVoE Control Server alongside RX decoders.
2. **Detect encoder "video alive" transitions** — when encoder goes from no-video to has-video.
3. **Correlate encoder and decoder state** — when encoder has video, verify all routed decoders are displaying.
4. **Flag anomalies** — if encoder has video but a decoder doesn't recover within a configurable window, mark it as `ANOMALY` (distinct from `FAIL`).
5. **Log encoder telemetry** in CSV alongside decoder data for post-analysis.
6. **Show encoder status in the UI** so operators see the full TX→RX pipeline.

### Non-Goals

- Controlling or commanding the encoder (read-only monitoring).
- Replacing the existing reboot cycle automation (that's a separate app).
- Modifying how decoders are discovered or selected.

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     SDVoE Control Server                     │
│              (GET_ALL_SETTINGS for TX and RX)                │
└──────────────┬──────────────────────┬───────────────────────┘
               │                      │
         TX Queries               RX Queries
               │                      │
               ▼                      ▼
┌──────────────────────┐  ┌──────────────────────┐
│    SdvoeService      │  │    SdvoeService       │
│  QueryDevicesAsync() │  │ SampleDecoderTelemetry│
│  SampleEncoderAsync()│  │        Async()        │
└──────────┬───────────┘  └──────────┬────────────┘
           │                         │
           ▼                         ▼
┌──────────────────────┐  ┌──────────────────────┐
│ EncoderTelemetry     │  │ DecoderTelemetry     │
│     Sample           │  │     Sample           │
└──────────┬───────────┘  └──────────┬────────────┘
           │                         │
           └──────────┬──────────────┘
                      ▼
         ┌────────────────────────┐
         │  AnomalyDetector       │
         │  (State Machine)       │
         │                        │
         │  Encoder alive?        │
         │    → Start window      │
         │    → Check decoders    │
         │    → Flag ANOMALY      │
         └────────────┬───────────┘
                      │
              ┌───────┴───────┐
              ▼               ▼
     ┌──────────────┐  ┌──────────────┐
     │ CSV Logging  │  │   UI/VM      │
     │ (encoder +   │  │ (encoder     │
     │  anomalies)  │  │  status +    │
     └──────────────┘  │  anomaly     │
                       │  indicators) │
                       └──────────────┘
```

---

## 4. Data Model

### 4.1 `EncoderTelemetrySample`

New model class for encoder telemetry snapshots. Mirrors the structure of `DecoderTelemetrySample` but captures TX-specific fields.

**File:** `src/AVASDecoderChecker/Models/EncoderTelemetrySample.cs`

```csharp
public class EncoderTelemetrySample
{
    // Identity
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string EncoderName { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string IpAddress { get; set; } = "";

    // Video Source Status
    public bool IsSourceStable { get; set; }
    public bool HasVideo { get; set; }
    public string VideoRaster { get; set; } = "No Raster";
    public double PixelClockMhz { get; set; }
    public string ColorFormat { get; set; } = "";

    // Streaming Status
    public bool IsStreaming { get; set; }
    public string StreamState { get; set; } = "UNKNOWN";
    public string MulticastAddress { get; set; } = "0.0.0.0";

    // HDCP
    public bool IsHdcpProtected { get; set; }

    // Dual-Link
    public string LinkMode { get; set; } = "SINGLE";
    public string LinkStatus { get; set; } = "UNKNOWN";
    public string CompanionMac { get; set; } = "";

    // Hardware Health (new fields from diagnostic review)
    public int Temperature { get; set; }
    public bool HasErrorCode { get; set; }
    public int ErrorCode { get; set; }

    // Overall
    public string OverallStatus { get; set; } = "UNKNOWN";
    public string Notes { get; set; } = "";
}
```

### 4.2 `EncoderItem`

New model for encoder discovery, parallel to `DecoderItem`.

**File:** `src/AVASDecoderChecker/Models/EncoderItem.cs`

```csharp
public class EncoderItem : INotifyPropertyChanged
{
    public string MacAddress { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool IsOnline { get; set; }
    public bool IsTransmitter { get; set; } = true;
    public string CompanionMac { get; set; } = "";  // dual-link partner
}
```

### 4.3 Extensions to `DecoderTelemetrySample`

Add fields to existing model to support encoder correlation and anomaly detection:

```csharp
// Add to DecoderTelemetrySample:
public string SourceEncoderStatus { get; set; } = "UNKNOWN";
    // "HAS_VIDEO", "NO_VIDEO", "UNKNOWN"
public bool IsAnomaly { get; set; }
    // true when encoder has video but decoder doesn't display
public double RecoveryTimeSec { get; set; }
    // seconds from encoder-alive to decoder-displaying (0 if not applicable)
```

### 4.4 Extensions to `AppSettings`

```csharp
// Add to AppSettings:
public double AnomalyWindowSeconds { get; set; } = 10.0;
    // seconds to wait after encoder comes alive before flagging anomaly
```

---

## 5. Service Layer Changes

### 5.1 `SdvoeService` — Encoder Discovery & Telemetry

**Encoder Discovery:** Extend the existing discovery flow to also return TX devices.

```csharp
// New method:
public async Task<List<EncoderItem>> QueryEncodersAsync(
    string ip, int port, int timeoutMs = 5000)
```

Logic:
1. Call `voips.DiscoveryIdentity()` (same as decoder discovery).
2. Filter devices where `identity.is_transmitter == true` (instead of `is_receiver`).
3. Return `List<EncoderItem>` with MAC, name, IP, online status.
4. For dual-link encoders, detect companion from `MULTI_LINK_TRANSMITTER` node.

**Encoder Telemetry:** New method to sample encoder state.

```csharp
// New method:
public async Task<EncoderTelemetrySample> SampleEncoderTelemetryAsync(
    string ip, int port, EncoderItem encoder, int timeoutMs = 3000)
```

Logic:
1. Call `GET_ALL_SETTINGS(encoder.MacAddress)` — same SDK call as decoders.
2. Parse the JSON response using a new `EvaluateEncoder()` method.
3. Extract encoder-specific fields from the TX device's node tree:
   - `nodes[type == "HDMI_DECODER"].status.source_stable` — on TX devices, the HDMI input port is an `HDMI_DECODER` node type (it decodes the HDMI input signal from the video source).
   - `nodes[type == "HDMI_DECODER"].status.Video.*` — video raster from the source.
   - `nodes[type == "HDMI_DECODER"].status.video_details.*` — pixel clock, frame rate.
   - `streams[type == "HDMI"].status.state` — whether the encoder is streaming to the network.
   - `streams[type == "HDMI"].configuration.address` — multicast address being transmitted.
   - `nodes[type == "MULTI_LINK_TRANSMITTER"].status.link_mode/link_status` — dual-link sync.
   - `status.temperature` — encoder temperature.
   - `status.error_status.*` — error codes.

**Key difference from decoder evaluation:** On an encoder (TX), the HDMI input is an `HDMI_DECODER` node (it receives video FROM the source), and the network output is in `streams[]` (it sends video TO the network). This is the reverse of an RX decoder where `subscriptions[]` receive FROM the network and `HDMI_ENCODER` sends TO the display.

### 5.2 `HealthEvaluator` — Encoder Evaluation

New static method:

```csharp
public static EncoderTelemetrySample EvaluateEncoder(
    string rawJson, EncoderItem encoder)
```

Evaluation logic:
1. Parse JSON, locate the device object.
2. Extract `HDMI_DECODER` node (input from video source):
   - `source_stable` → `IsSourceStable`
   - `Video.width/height` + `video_details.frame_rate` → `VideoRaster`
   - `video_details.pixel_clock` → `PixelClockMhz`
3. Extract `streams[HDMI:0]`:
   - `status.state` → `StreamState` / `IsStreaming`
   - `configuration.address` → `MulticastAddress`
4. Determine `HasVideo`:
   - `HasVideo = IsSourceStable && PixelClockMhz > 0 && IsStreaming`
5. Extract `MULTI_LINK_TRANSMITTER` if present:
   - `link_mode`, `link_status`, `companions`
6. Extract `status.temperature`, `status.error_status`.
7. Set `OverallStatus`:
   - `"PASS"` if `HasVideo && IsStreaming`
   - `"FAIL"` if `!HasVideo || !IsStreaming`
   - `"WARN"` if `HasVideo && !IsSourceStable` (partial lock)

### 5.3 `HealthEvaluator` — Encoder-Aware Decoder Evaluation

Extend `EvaluateParsedJObject()` to accept optional encoder state:

```csharp
public static DecoderTelemetrySample EvaluateParsedJObject(
    JObject deviceObj,
    DecoderItem decoder,
    EncoderTelemetrySample? encoderState = null)
```

When `encoderState` is provided:
1. Set `sample.SourceEncoderStatus` based on encoder's `HasVideo`.
2. Populate `sample.SourceEncoderMac` from `encoderState.MacAddress` (fixes the current bug where it's always `N/A`).

The anomaly detection itself is NOT in HealthEvaluator — it's in the dedicated `AnomalyDetector` (see below). HealthEvaluator just annotates samples with the encoder context.

---

## 6. Anomaly Detection

### 6.1 `AnomalyDetector` Service

**File:** `src/AVASDecoderChecker/Services/AnomalyDetector.cs`

A lightweight state machine that tracks encoder video transitions and correlates them with decoder state.

```csharp
public class AnomalyDetector
{
    // Per-encoder state tracking
    private readonly ConcurrentDictionary<string, EncoderState> _encoderStates = new();

    // Configuration
    public double AnomalyWindowSeconds { get; set; } = 10.0;

    // Events
    public event EventHandler<AnomalyEvent>? AnomalyDetected;
}
```

**State machine per encoder:**

```
                    ┌──────────────┐
        ┌──────────│  NO_VIDEO    │◄──────────────────┐
        │          └──────┬───────┘                    │
        │                 │                            │
        │    encoder.HasVideo                encoder.HasVideo
        │    becomes true                    becomes false
        │                 │                            │
        │                 ▼                            │
        │          ┌──────────────┐                    │
        │          │  RECOVERY    │                    │
        │          │  WINDOW      │                    │
        │          │ (timer = 0)  │                    │
        │          └──────┬───────┘                    │
        │                 │                            │
        │      ┌──────────┴──────────┐                 │
        │      │                     │                 │
        │   all decoders          timer > X sec        │
        │   displaying            and decoder(s)       │
        │                         NOT displaying       │
        │      │                     │                 │
        │      ▼                     ▼                 │
        │ ┌──────────┐     ┌──────────────┐           │
        │ │ HEALTHY   │     │  ANOMALY     │           │
        │ │ (log      │     │ (flag        │           │
        │ │ recovery  │     │  affected    │           │
        │ │  time)    │     │  decoders)   │           │
        │ └──────┬────┘     └──────┬───────┘           │
        │        │                 │                   │
        │        └─────────────────┘                   │
        │                 │                            │
        │          continues monitoring                │
        │          (stays in HAS_VIDEO                 │
        │           until encoder drops)               │
        │                 │                            │
        └─────────────────┴────────────────────────────┘
```

**State tracking data:**

```csharp
private class EncoderState
{
    public bool PreviousHasVideo { get; set; }
    public DateTime? VideoAliveTimestamp { get; set; }  // when HasVideo became true
    public bool InRecoveryWindow { get; set; }
    public HashSet<string> RecoveredDecoders { get; set; } = new();
}
```

### 6.2 Core Logic — `ProcessCycle()`

Called once per polling cycle after both encoder and decoder samples are collected:

```csharp
public void ProcessCycle(
    List<EncoderTelemetrySample> encoderSamples,
    List<DecoderTelemetrySample> decoderSamples)
```

Steps:
1. **Determine composite encoder status.** For dual-link encoders, `HasVideo` requires BOTH chips to report `source_stable == true` (since both must be active for full 4K).
2. **Detect transition.** Compare current `HasVideo` against `PreviousHasVideo`:
   - `false → true`: Start recovery window. Set `VideoAliveTimestamp = now`.
   - `true → false`: Reset state. Any open anomaly window is closed.
3. **During recovery window:** For each decoder routed to this encoder:
   - If decoder's `DisplayScreenStatus == "DISPLAYING_VIDEO"`, add to `RecoveredDecoders`.
   - If all decoders recovered, close window. Log recovery time = `now - VideoAliveTimestamp`.
4. **Window expired:** If `(now - VideoAliveTimestamp) > AnomalyWindowSeconds` and NOT all decoders recovered:
   - For each non-recovered decoder: Set `sample.IsAnomaly = true`, `sample.OverallStatus = "ANOMALY"`.
   - Fire `AnomalyDetected` event with details.
   - Keep checking on subsequent polls (anomaly persists until decoder recovers or encoder drops).

### 6.3 Encoder-to-Decoder Routing

The app needs to know which decoders are routed to which encoder. This is determined by `subscriptions[HDMI:0].configuration.source.device_id` on the decoder side — it contains the encoder's MAC address.

Since `SourceEncoderMac` is currently always `N/A` (a bug), fixing this extraction is a prerequisite for anomaly correlation. The fix: in `EvaluateParsedJObject`, the `source` field in the subscription can be either a `JValue` (string MAC) or a `JObject` with a `device_id` property. Handle both cases.

For the initial implementation, if the source MAC cannot be determined, fall back to correlating by multicast address (decoders subscribed to the same multicast group as an encoder's stream output are considered "routed").

### 6.4 `AnomalyEvent` Model

```csharp
public class AnomalyEvent
{
    public DateTime Timestamp { get; set; }
    public string EncoderMac { get; set; } = "";
    public string EncoderName { get; set; } = "";
    public DateTime EncoderAliveTimestamp { get; set; }
    public double ElapsedSeconds { get; set; }
    public List<string> AffectedDecoderMacs { get; set; } = new();
    public List<string> AffectedDecoderNames { get; set; } = new();
    public string Description { get; set; } = "";
}
```

---

## 7. MonitorEngine Changes

### 7.1 Enhanced Polling Loop

The `PollingLoopAsync` method is extended to:

1. **Poll encoders first** (both dual-link chips), producing `List<EncoderTelemetrySample>`.
2. **Poll decoders** (same as today), producing `List<DecoderTelemetrySample>`.
3. **Pass encoder state to decoder evaluation** — `HealthEvaluator.EvaluateParsedJObject()` receives the relevant `EncoderTelemetrySample` so it can populate `SourceEncoderStatus` and `SourceEncoderMac`.
4. **Run anomaly detection** — call `AnomalyDetector.ProcessCycle()` with both lists.
5. **Log and emit events** for both encoder and decoder samples.

### 7.2 New Events

```csharp
public event EventHandler<EncoderTelemetrySample>? EncoderTelemetrySampleReceived;
public event EventHandler<AnomalyEvent>? AnomalyDetected;
```

### 7.3 Encoder List Management

The `StartAsync` method signature adds an optional encoder list:

```csharp
public async Task<string> StartAsync(
    AppSettings settings,
    List<DecoderItem> selectedDecoders,
    List<EncoderItem>? encoders = null,  // NEW
    string? baseLogsDir = null)
```

If `encoders` is null or empty, the engine auto-discovers encoders on first poll cycle.

---

## 8. CSV Logging Changes

### 8.1 Encoder Telemetry CSV

New file per session: `encoder_telemetry.csv`

Header:
```
Timestamp,EncoderName,MAC,IP,OverallStatus,SourceStable,HasVideo,VideoRaster,PixelClockMhz,StreamState,MulticastAddress,LinkMode,LinkStatus,Temperature,ErrorCode,Notes
```

### 8.2 Master Telemetry CSV — New Columns

Add to existing master CSV:

```
...,SourceEncoderStatus,IsAnomaly,RecoveryTimeSec,...
```

- `SourceEncoderStatus`: `"HAS_VIDEO"`, `"NO_VIDEO"`, or `"UNKNOWN"`
- `IsAnomaly`: `True`/`False`
- `RecoveryTimeSec`: seconds from encoder-alive to this decoder displaying (0 if not in recovery window)

### 8.3 Anomaly Log

New file per session: `anomaly_events.csv`

Header:
```
Timestamp,EncoderAliveTimestamp,ElapsedSeconds,EncoderName,EncoderMAC,AffectedDecoderName,AffectedDecoderMAC,Description
```

One row per affected decoder per anomaly event.

### 8.4 Summary Report Enhancement

Add to `summary_report.csv`:
- Encoder section: encoder name, MAC, total samples, HasVideo percentage, average video-alive duration
- Anomaly section: total anomaly events, affected decoders, average recovery time for successful recoveries

---

## 9. UI Changes

### 9.1 Encoder Status Display

Add a compact encoder status section to the dashboard, above the existing decoder telemetry grid. Displays for each discovered encoder:

- Name / MAC / IP
- Video source status: green "VIDEO ACTIVE" or red "NO VIDEO"
- Streaming state: green "STREAMING" or red "STOPPED"
- Link mode: "DUAL" or "SINGLE"
- Temperature (if available)

This does not require a separate DataGrid — a compact card or status bar with key indicators is sufficient. Dual-link encoder chips are grouped together as one logical encoder.

### 9.2 Anomaly Status in Telemetry Grid

The existing LiveTelemetry DataGrid already shows decoder status. Add:

- `SourceEncoderStatus` column showing "HAS_VIDEO" / "NO_VIDEO" / "UNKNOWN"
- `IsAnomaly` column or visual indicator (distinct color)
- `ANOMALY` status in the OverallStatus column gets a distinct color: **magenta/purple** (`#A855F7`) — visually distinct from FAIL (red), WARN (amber), and PASS (green)

### 9.3 KPI Updates

Add or update KPI cards:
- **Encoder Status** card: "VIDEO ACTIVE" / "NO VIDEO" with green/red indicator
- **Anomaly Count** card: running count of anomaly events this session

### 9.4 Activity Log

Log significant events:
- "Encoder TX1 video source detected — starting recovery window (10.0s)"
- "All 4 decoders recovered in 3.2s — PASS"
- "⚠️ ANOMALY: Encoder TX1 has video but decoder RX1 (74fe488b22ca) not displaying after 10.0s"
- "Encoder TX1 video source lost"

---

## 10. Bug Fixes (Included in This Work)

These existing bugs directly impact the feature and must be fixed as part of this implementation:

### 10.1 Fix `SourceEncoderMac` Extraction

**Current:** Always `N/A` because the `source` field parsing doesn't handle the `JObject` format (`{"device_id": "...", "stream_index": 0}`).

**Fix:** In `HealthEvaluator.EvaluateParsedJObject()`, handle both formats:
```csharp
var sourceToken = hdmiSub["configuration"]?["source"];
if (sourceToken is JValue jv)
    sourceTxMac = jv.ToString();
else if (sourceToken is JObject jo)
    sourceTxMac = jo["device_id"]?.ToString() ?? "N/A";
```

### 10.2 Fix KPI String Mismatches

**Current:** ViewModel compares against `"RECEIVING"` / `"DISPLAYING"` but evaluator produces `"RECEIVED"` / `"DISPLAYING_VIDEO"`.

**Fix:** Update `MainViewModel.OnTelemetrySampleReceived()` to use correct strings:
```csharp
KpiVideoReceivedCount = LiveTelemetry.Count(t => t.VideoReceivedStatus == "RECEIVED");
KpiDisplaysActiveCount = LiveTelemetry.Count(t => t.DisplayScreenStatus == "DISPLAYING_VIDEO");
```

### 10.3 Fix Status Color Converters

**Current:** Many evaluator output strings are not in the converter switch statements.

**Fix:** Add all missing strings to `StatusToColorConverter` and `StatusToBgBrushConverter`:
- Green: `"RECEIVED"`, `"DISPLAYING_VIDEO"`, `"VIDEO RECEIVED"`, `"DISPLAYING VIDEO"`, `"HAS_VIDEO"`
- Amber: `"WAITING_FOR_SOURCE"`, `"WAITING FOR SOURCE"`, `"NO_VIDEO"` (for encoder context)
- Red: `"NO_STREAM"`, `"NO_DISPLAY"`, `"HANDSHAKE_FAILED"`, `"BLACK_SCREEN"`, `"NO VIDEO STREAM"`, `"NO CABLE / DISPLAY OFF"`, `"HANDSHAKE FAILED"`, `"BLACK SCREEN (HDCP)"`
- Purple/Magenta: `"ANOMALY"`

### 10.4 Distinguish SDK Timeout from Decoder Failure

**Current:** SDK timeouts logged as `FAIL` with generic error message.

**Fix:** When `GET_ALL_SETTINGS` times out or returns no response, set `OverallStatus = "POLL_TIMEOUT"` instead of `"FAIL"`. This is a communication error, not a decoder health failure.

---

## 11. Testing Strategy

### 11.1 Unit Tests — New

| Test | What It Verifies |
|------|-----------------|
| `EvaluateEncoder_WithStableSource_ReturnsHasVideo` | Encoder evaluation produces `HasVideo = true` when source_stable and pixel clock valid |
| `EvaluateEncoder_WithNoSource_ReportsNoVideo` | Encoder evaluation produces `HasVideo = false` when source_stable = false |
| `EvaluateEncoder_DualLink_RequiresBothChips` | Dual-link encoder only reports HasVideo when both chips are stable |
| `AnomalyDetector_EncoderAlive_AllDecodersRecover_NoAnomaly` | No anomaly flagged when all decoders recover within window |
| `AnomalyDetector_EncoderAlive_DecoderNotRecovered_FlagsAnomaly` | Anomaly flagged when decoder doesn't recover within window |
| `AnomalyDetector_EncoderDrops_ResetsWindow` | Recovery window cancels when encoder loses video |
| `AnomalyDetector_WindowExpires_AnomalyPersists` | Anomaly status persists until decoder recovers |
| `SourceEncoderMac_ParsedFromJObject` | Fixes the N/A bug — source MAC extracted from `{"device_id": "..."}` format |
| `SourceEncoderMac_ParsedFromJValue` | Source MAC extracted from plain string format |

### 11.2 Unit Tests — Bug Fix Verification

| Test | What It Verifies |
|------|-----------------|
| `KpiCounts_UseCorrectStatusStrings` | ViewModel counts use `"RECEIVED"` / `"DISPLAYING_VIDEO"` |
| `StatusConverter_RecognizesAllEvaluatorStrings` | All evaluator output strings map to correct colors |
| `SdkTimeout_ReportsPollTimeout_NotFail` | Timeout produces `"POLL_TIMEOUT"` not `"FAIL"` |
| `AnomalyStatus_MapsToMagentaColor` | `"ANOMALY"` maps to purple/magenta in converters |

### 11.3 Integration Test Scenarios

Using golden JSON payloads:

| Scenario | Expected Result |
|----------|----------------|
| Encoder TX has video, all RX displaying | All PASS, no anomaly |
| Encoder TX has video, one RX not displaying | RX flagged ANOMALY after window |
| Encoder TX no video, all RX no video | All FAIL (expected, not anomaly) |
| Encoder TX transitions no-video → video, RX recovers in 3s | Recovery time logged as 3.0s |
| Encoder TX transitions no-video → video, RX never recovers | ANOMALY flagged after window |
| SDK timeout during poll | POLL_TIMEOUT status, not FAIL |

---

## 12. File Inventory

| File | Action | Description |
|------|--------|-------------|
| `Models/EncoderTelemetrySample.cs` | **NEW** | Encoder telemetry data model |
| `Models/EncoderItem.cs` | **NEW** | Encoder discovery model |
| `Models/DecoderTelemetrySample.cs` | **MODIFY** | Add SourceEncoderStatus, IsAnomaly, RecoveryTimeSec |
| `Models/AppSettings.cs` | **MODIFY** | Add AnomalyWindowSeconds |
| `Services/AnomalyDetector.cs` | **NEW** | Anomaly detection state machine |
| `Services/IAnomalyDetector.cs` | **NEW** | Interface for AnomalyDetector |
| `Services/SdvoeService.cs` | **MODIFY** | Add QueryEncodersAsync, SampleEncoderTelemetryAsync |
| `Services/ISdvoeService.cs` | **MODIFY** | Add encoder method signatures |
| `Services/HealthEvaluator.cs` | **MODIFY** | Add EvaluateEncoder, fix SourceEncoderMac, accept encoder context |
| `Services/MonitorEngine.cs` | **MODIFY** | Poll encoders, run anomaly detection |
| `Services/IMonitorEngine.cs` | **MODIFY** | Add encoder events |
| `Services/CsvLoggingService.cs` | **MODIFY** | Log encoder CSV, anomaly CSV, add columns |
| `Services/ILoggingService.cs` | **MODIFY** | Add encoder/anomaly logging methods |
| `ViewModels/MainViewModel.cs` | **MODIFY** | Encoder display, anomaly KPIs, fix string mismatches |
| `Converters/StatusConverters.cs` | **MODIFY** | Add missing status strings, ANOMALY color |
| `MainWindow.xaml` | **MODIFY** | Encoder status section, anomaly column |
| `Tests/EncoderEvaluationTests.cs` | **NEW** | Encoder evaluation tests |
| `Tests/AnomalyDetectorTests.cs` | **NEW** | Anomaly detection tests |
| `Tests/BugFixTests.cs` | **NEW** | Bug fix verification tests |

**New files:** 5  
**Modified files:** 13  
**Total:** 18

---

## 13. Configuration

### `config/settings.json` Additions

```json
{
    "AnomalyWindowSeconds": 10.0
}
```

The anomaly window default of 10 seconds allows for:
- SDVoE multicast stream propagation (~1–2s)
- Decoder genlock acquisition (~2–5s)
- HDMI handshake + EDID negotiation (~1–3s)
- Safety margin (~2–4s)

This is configurable in the UI settings panel.

---

## 14. Rollout Considerations

- **Backward compatible:** Existing CSV logs remain readable. New columns are appended.
- **No encoder = no anomaly detection:** If no TX devices are found on the SDVoE server, the app works exactly as before. Anomaly detection is simply inactive.
- **Dual-link awareness:** Both encoder chips are grouped as one logical encoder. `HasVideo` requires BOTH to be stable for 4K content.
- **Polling performance:** Adding 2 encoder queries per cycle adds ~300ms to the polling loop (4 decoders + 2 encoders = ~6 queries × 150ms = ~900ms total per cycle). With a 2s polling interval, this leaves adequate headroom.
