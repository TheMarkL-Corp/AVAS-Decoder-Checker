# Software Specification: AVAS Decoder (RX) HDMI Stream Checker

> **Document Version:** 1.0.2  
> **Release Target:** v1.0.2  
> **Date:** September 26, 2026  
> **Repository:** [TheMarkL-Corp/AVAS-Decoder-Checker](https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker)  
> **Target Framework:** C# .NET 8.0 Windows Desktop (`net8.0-windows`)  
> **Architecture Pattern:** MVVM (Model-View-ViewModel) + Service-Oriented Architecture  

---

## 1. System Overview & Objective

### 1.1 Purpose
The **AVAS Decoder (RX) HDMI Stream Checker** is an automated diagnostic and continuous monitoring desktop application engineered specifically for **AVAS / SDVoE (Software Defined Video Over Ethernet)** hardware platforms (e.g., Aquantia/Semtech AVP2000 / BlueRiver chipset solutions).

In automated video validation rigs and commercial A/V deployments, video source computers are frequently rebooted (e.g., on 60–80 second power-cycling routines) or operated under high stress. During these cycles, display monitors connected to SDVoE decoders occasionally suffer from **intermittent black screens**, delayed recovery, or permanent lockups.

Before this system was implemented, troubleshooting these incidents was severely limited:
- Operators could not distinguish between a normal outage caused by the source PC rebooting (~44.5s) versus an abnormal decoder failure.
- When a display went black, it was impossible to isolate whether the failure originated in:
  1. The **Video Source / Upstream Transmitter (TX)** (no video generated or transmitted),
  2. The **SDVoE Decoder (RX)** (network stream dropped, PLL multiplier miscalculated, or dual-link desynchronized),
  3. The **Connected Display / Sink** (HDMI Hot-Plug Detect dropped, monitor in deep sleep, EDID handshake corrupted, or HDCP revoked), or
  4. The **Control Infrastructure** (SDVoE control server unresponsive or network switch link down).

### 1.2 Core Objectives
1. **Upstream Transmitter (TX / Encoder) Telemetry Sampling**: Continuously track HDMI source lock, pixel clocks, rasters, and chip thermals directly from upstream encoders, pairing dual-link transmission chips into unified composite health signals.
2. **Automated Anomaly Detection State Machine**:
   - Enforce a strict **2.0-second post-reboot recovery window** (`AnomalyWindowSeconds = 2.0`). When the upstream video source boots and the encoder acquires video, flag any decoder that fails to restore display output within 2.0s as an `ANOMALY` (`POST_REBOOT_TIMEOUT`).
   - Detect **intermittent mid-stream blackouts**. When the encoder continuously outputs video (`HasVideo == true`), detect sudden dropouts on previously active displays, calculate exact blackout duration, track whether the display recovered before the next reboot cycle (`MIDSTREAM_RECOVERED`), and log incident details to `anomaly_events.csv`.
3. **Definitive Root-Cause Fault Isolation**: Evaluate hardware telemetry against an exhaustive decision matrix to attribute faults into discrete categories (`SOURCE_REBOOTING`, `POST_REBOOT_RECOVERY_TIMEOUT`, `DECODER_PLL_DESYNC`, `DECODER_DUAL_DESYNC`, `DECODER_STREAM_LOSS`, `DISPLAY_HPD_DOWN`, `DISPLAY_EDID_CORRUPT`, `DISPLAY_HDCP_BLOCKED`, `SERVER_TIMEOUT`).
4. **Deep Hardware Telemetry Ingestion**: Parse VESA EDID 128-byte hex blocks over DDC to extract monitor make, model, serial number, and preferred timings; monitor internal chip temperatures with >68°C warnings; evaluate dual-link AVP2000 companion sync; verify 10G network ports.
5. **Multi-Tier Run-Isolated CSV Logging**: Automatically generate a dedicated, timestamped folder per test run (`logs/Test_YYYYMMDD_HHmmss/`) containing unified master telemetry, dedicated encoder telemetry, anomaly incident ledgers, individual decoder time-series files, and comprehensive run summaries.

---

## 2. High-Level Architecture & Component Model

The system follows a clean MVVM (Model-View-ViewModel) architecture with decoupled background service layers:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           Presentation Layer (WPF UI)                           │
│  ┌───────────────────────┐  ┌─────────────────────────┐  ┌───────────────────┐  │
│  │   Server Controls     │  │   Upstream TX Card      │  │ Live KPI Counters │  │
│  │   & Port Probing      │  │   Pipeline Status       │  │ (PASS/WARN/FAIL/  │  │
│  │   (IP/Port/Verify)    │  │   (Raster/Clock/Temp)   │  │  ANOMALIES/Time)  │  │
│  └───────────────────────┘  └─────────────────────────┘  └───────────────────┘  │
│  ┌────────────────────────────────────────────────────┐  ┌───────────────────┐  │
│  │   Reactive Telemetry DataGrid (Filter/Select All)   │  │ Hardware Inspector│  │
│  │   Columns: Fault Attribution, Recovery & Anomaly   │  │ (EDID, HDCP, DDC) │  │
│  └────────────────────────────────────────────────────┘  └───────────────────┘  │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │ Data Binding & RelayCommands
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        ViewModel Layer (MainViewModel)                          │
│   - Orchestrates UI state, background events, and user interactions             │
│   - Thread-safe UI dispatching via Application.Current.Dispatcher               │
│   - Maintains ObservableCollections for Decoders, Encoders, and Activity Logs   │
└──────────────────┬─────────────────────────────────────────────┬────────────────┘
                   │                                             │
                   ▼                                             ▼
┌──────────────────────────────────────┐     ┌────────────────────────────────────┐
│      MonitorEngine (Orchestrator)     │     │       SettingsService              │
│  - Periodic polling task loop        │     │  - Reads/writes settings.json      │
│  - Cancellation & elapsed stopwatch  │     │  - Preserves selected decoders     │
│  - Dispatches samples & events       │     └────────────────────────────────────┘
└──────┬────────────────────────┬──────┘
       │                        │
       ▼                        ▼
┌──────────────────────┐  ┌──────────────────────┐  ┌─────────────────────────────┐
│    SdvoeService      │  │   AnomalyDetector    │  │     CsvLoggingService       │
│  - VOIPS.dll interop │  │   State Machine      │  │  - Isolated run directories │
│  - TCP port probing  │  │  - 2.0s recovery     │  │  - Thread-safe file writes  │
│  - REST fallback     │  │  - Midstream blackouts│ │  - Master, TX, Anomaly, RX  │
│  - JSON parsing      │  │  - Event ledger      │  │  - Summary statistics       │
└──────┬───────────────┘  └──────────┬───────────┘  └─────────────────────────────┘
       │                             │
       ▼                             ▼
┌─────────────────────────────────────────────────┐
│             HealthEvaluator Engine              │
│  - Two-tier question logic (Inbound / Outbound) │
│  - Root-cause fault isolation classification    │
│  - PLL glitch detection & transmitter analysis  │
└─────────────────────────────────────────────────┘
```

---

## 3. Communication & Control Layer (`SdvoeService`)

### 3.1 Control Server Connectivity & Dual-Port Negotiation
The SDVoE Control Server typically runs on port `8090` (WebSocket / JSON-RPC) or port `80` (HTTP REST). `SdvoeService` implements intelligent connection handling:
1. **Effective Host Resolution**: Resolves hostnames or `localhost` to IPv4 loopback (`127.0.0.1`) to prevent IPv6 / IPv4 dual-stack resolution delays.
2. **Proactive TCP Socket Probing**: Performs a low-latency TCP connect probe (`CheckTcpPortAsync`) with a 1500ms timeout.
3. **Automatic Alternate Port Fallback**: If the configured port is closed, the service automatically probes the companion standard port (`8090` -> `80` or `80` -> `8090`). If the alternative port succeeds, it transparently switches to the active port and alerts the operator.
4. **SDK Initialization & Interop**: Initializes the unmanaged/managed bridge `VOIPS.dll` (`VOIPS_LIB.VOIPS`), setting the server target via `SET_CONTROL_SERVER(ip, port)`.

### 3.2 Asynchronous JSON-RPC Workflow
When querying full device state (`GET_ALL_SETTINGS`):
- Many SDVoE control servers return an immediate HTTP/JSON response with `status: "SUCCESS"` and the embedded device object.
- When querying large switch fabrics or busy servers, the server responds with:
  ```json
  { "status": "PROCESSING", "request_id": 4812 }
  ```
- `SdvoeService` seamlessly enters a polling loop, calling `voips.GET_REQUEST(requestId)` every 150ms until the deadline (`timeoutMs`, default 3000ms–5000ms) expires or the device payload is received.
- **Direct REST Fallback**: If `VOIPS.dll` encounters an unhandled driver or communication exception, the service falls back to a direct HTTP GET request to `http://<ip>:<port>/api/devices/<mac>` using a shared `HttpClient`.

---

## 4. Upstream Transmitter (TX / Encoder) Telemetry (`HealthEvaluator.EvaluateEncoder`)

### 4.1 Device Discovery & Identification
Transmitters are discovered concurrently with decoders via `voips.DiscoveryIdentity()` and filtered by:
```csharp
bool isTx = dev.identity?.is_transmitter ?? (dev.identity?.is_receiver == false);
```
Each discovered transmitter is mapped into an `EncoderItem` model, capturing:
- `MacAddress`: Hardware MAC (e.g., `74:fe:48:8b:07:bb`)
- `DeviceName`: User-friendly configuration alias or MAC fallback
- `IpAddress`: Extracted via MCU IP query (`voips.GET_MCU_IP`) or network interface node
- `MulticastAddress`: Target multicast IP (e.g., `224.1.1.200`) extracted from `streams[HDMI]`

### 4.2 Telemetry Attribute Ingestion
On SDVoE transmitters, the physical HDMI input port from the source PC is represented as an **`HDMI_DECODER`** node:
- **Source Stability** (`HDMI_DECODER.status.source_stable`): Indicates whether the transmitter's internal HDMI receiver has locked onto the incoming TMDS clock from the source PC graphics card.
- **Pixel Clock** (`video_details.pixel_clock`): Incoming pixel clock frequency converted to MHz (e.g., 297.00 MHz for 4K30 or 594.00 MHz for 4K60).
- **Video Raster** (`video_details.width`, `height`, `fps`): Synthesizes formatted raster string (e.g., `3840x2160@60Hz`).
- **Color Format** (`Video.color_space`): Colorimetry and chroma subsampling (e.g., `RGB`, `YCbCr444`, `YCbCr420`).
- **HDCP State** (`status.hdcp_protected`): Encryption flag asserted by source.
- **Internal Temperature** (`status.temperature`): Internal ASIC die temperature.
- **Error Code** (`status.error_status.code`): Hardware diagnostic error bitmask.
- **Multicast Stream State** (`streams[HDMI].status.state`): Must be `"STREAMING"` or `"ACTIVE"`.

### 4.3 Composite Transmitter Stability & Dual-Link Pairing
For high-bandwidth feeds (e.g., 4K60 uncompressed or dual-head sources), transmitters operate in **Dual-Link mode** (`MULTI_LINK_TRANSMITTER`), splitting transmission across two physical MACs (primary and companion, e.g. `74fe488b07bb` and `74fe488b07bc`):
- `HealthEvaluator` dynamically links companion MACs via `configuration.companions`.
- Composite stability rule:
  ```csharp
  bool currentCompositeHasVideo = encoderSamples.Count > 0 && encoderSamples.All(e => e.HasVideo);
  ```
- If either transmission chip in a dual-link pair loses lock, `currentCompositeHasVideo` is evaluated as `false`. This prevents false anomaly flags when an upstream cable or transmitter half-fails.

---

## 5. Decoder (RX) Telemetry & Stream Health Ingestion (`HealthEvaluator.EvaluateParsedJObject`)

### 5.1 Inbound Stream Telemetry (Question 1)
Determines whether video is successfully arriving at the decoder from the network:
1. **Subscription Inspection** (`subscriptions[HDMI]`):
   - Validates that the decoder is subscribed to an active multicast address (`address != "0.0.0.0"`).
   - Validates subscription state (`state == "STREAMING" || state == "ACTIVE" || state == "CONNECTED"`).
   - Resolves the upstream transmitter MAC (`SourceEncoderMac`) either directly from `configuration.source` or via reverse lookup of the multicast IP in `_multicastToEncoderMap`.
2. **Clock Stability** (`HDMI_ENCODER.status.source_stable`):
   - Confirms that the decoder's AVP2000 genlock PLL is locked to the network stream.
3. **Raster & Pixel Clock**:
   - Ingests active video parameters from `video_details` (`width`, `height`, `fps`, `pixel_clock`).
   - Handles dual-link receiver mode (`MULTI_LINK_RECEIVER`): if `link_mode == "DUAL"` and input is reported as half-raster `1920x2160`, synthesizes composite 4K raster `3840x2160`.

### 5.2 Outbound Display Sink Telemetry (Question 2)
Determines whether the connected display is actively rendering video:
1. **Physical Hot-Plug Detect (HPD)** (`HDMI_MONITOR.status.connected`):
   - Inspects the physical 5V Hot-Plug Detect pin (Pin 19 of the HDMI connector).
   - If `false`, the display is physically disconnected, powered off, or in deep sleep.
2. **VESA EDID Hex Parsing (`EdidParser`)**:
   - Ingests the 128-byte raw hexadecimal EDID string from `HDMI_MONITOR.status.edid`.
   - **Header Validation**: Verifies fixed magic bytes `00 FF FF FF FF FF FF 00`.
   - **Manufacturer ID**: Unpacks compressed 5-bit big-endian characters at bytes 0x08–0x09 (e.g., `VSC` for ViewSonic, `DEL` for Dell, `SAM` for Samsung).
   - **Product Code & Serial Number**: Parses 16-bit little-endian product ID and 32-bit integer serial number.
   - **Detailed Timing Descriptor (DTD)**: Parses preferred native timing from bytes 0x36–0x47 (Pixel Clock, H-Active, H-Blank, V-Active, V-Blank).
   - **Descriptor Blocks**: Iterates 18-byte descriptors (0x36, 0x48, 0x5A, 0x6C) to locate Model Name (Tag `0xFC`) and ASCII Serial Number (Tag `0xFF`).
   - **Extension Blocks**: Detects CEA-861 audio support from extension flags at byte 0x7E.
   - **Checksum**: Calculates modulo-256 additive checksum across all 128 bytes.
3. **TMDS Clock Activity**:
   - Verifies that the physical TMDS clock generator is actively oscillating (`(pixelClock > 0 || width > 0) && clockStable`).
4. **HDCP Revocation & Blanking**:
   - Detects content-protection handshake failures (`hdcp_protected == true` but sink authentication rejected), resulting in muted black screen output.

---

## 6. Diagnostic Questions & Root-Cause Fault Attribution Matrix

### 6.1 Two-Tier Diagnostic Questions

| Dimension | Status Code | Badge Label | Diagnostic Criteria |
|---|---|---|---|
| **Question 1: Inbound** | `RECEIVED` | `VIDEO RECEIVED` | Route subscribed, multicast active, clock locked, raster valid. |
| | `NO_STREAM` | `NO VIDEO STREAM` | Route subscribed, but genlock clock unlocked or stream paused. |
| | `UNSUBSCRIBED` | `UNSUBSCRIBED` | Decoder subscription disabled or set to `0.0.0.0`. |
| **Question 2: Outbound** | `DISPLAYING_VIDEO` | `DISPLAYING VIDEO` | HPD high, EDID valid, TMDS active, inbound video received. |
| | `NO_DISPLAY` | `NO CABLE / DISPLAY OFF` | HPD low (cable disconnected or monitor powered off). |
| | `HANDSHAKE_FAILED`| `HANDSHAKE FAILED` | HPD high, but EDID checksum invalid or unreadable via DDC. |
| | `BLACK_SCREEN` | `BLACK SCREEN (HDCP)` | Display connected, but output blanked due to HDCP failure. |
| | `WAITING_FOR_SOURCE`| `WAITING FOR SOURCE`| Display ready, but no stream received from encoder. |

### 6.2 Root-Cause Fault Attribution Codes (`FaultAttribution`)

When an issue occurs, `HealthEvaluator.ComputeOverall` evaluates the entire hardware state to attribute the fault to one of ten definitive categories:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Fault Attribution Engine                        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
    ┌───────────────────────────────┼──────────────────────────────┐
    ▼                               ▼                              ▼
[ Decoder Faults ]          [ Display Faults ]            [ Source/Network ]
- DECODER_PLL_DESYNC        - DISPLAY_HPD_DOWN            - SOURCE_REBOOTING
- DECODER_DUAL_DESYNC       - DISPLAY_EDID_CORRUPT        - POST_REBOOT_RECOVERY_TIMEOUT
- DECODER_STREAM_LOSS       - DISPLAY_HDCP_BLOCKED        - SERVER_TIMEOUT
```

#### Detailed Definitions:
1. `NONE`: Normal, fully healthy operation. Inbound video locked and actively displaying on authenticated monitor.
2. `SOURCE_REBOOTING`: Video source PC is rebooting on its normal test cycle (`HDMI_DECODER.source_stable == false`). The decoder is healthy and waiting for the source signal to return.
3. `POST_REBOOT_RECOVERY_TIMEOUT`: Source PC rebooted and transmitter re-acquired video, but this specific decoder took longer than the configured recovery window (2.0s) to restore video output.
4. `DECODER_PLL_DESYNC`: AVP2000 hardware PLL multiplier miscalculation glitch. Detected when the reported pixel clock locks into anomalous frequencies at ~83.8% of normal HDMI clock rates:
   - Window 1: `240.0 MHz <= PixelClock <= 255.0 MHz` (glitch state of 297.0 MHz 4K30)
   - Window 2: `490.0 MHz <= PixelClock <= 505.0 MHz` (glitch state of 594.0 MHz 4K60)
   - Window 3: `390.0 MHz <= PixelClock <= 405.0 MHz`
5. `DECODER_DUAL_DESYNC`: Multi-link companion receiver (`MULTI_LINK_RECEIVER`) lost synchronization (`link_status != "SYNCHRONIZED"`).
6. `DECODER_STREAM_LOSS`: Decoder unexpectedly unsubscribed or dropped out of the multicast stream group.
7. `DISPLAY_HPD_DOWN`: HDMI cable disconnected, broken pin 19, or display panel entered sleep/standby mode (`isHpdConnected == false`).
8. `DISPLAY_EDID_CORRUPT`: Monitor connected via HDMI but failed DDC I2C handshake or corrupted VESA EDID block (`EdidInfo.IsValid == false`).
9. `DISPLAY_HDCP_BLOCKED`: Content stream blocked due to failed HDCP authentication or repeater handshake rejection.
10. `SERVER_TIMEOUT`: SDVoE control server unresponsive or network socket dropped.

---

## 7. Anomaly Detection State Machine (`AnomalyDetector`)

The `AnomalyDetector` operates as a high-frequency real-time state machine evaluated on every polling cycle:

```
                    ┌────────────────────────────┐
                    │  Initial State / Monitoring │
                    └─────────────┬──────────────┘
                                  │
          ┌───────────────────────┴───────────────────────┐
          │                                               │
          ▼                                               ▼
[ TX Video Loss: HasVideo -> false ]       [ TX Video Restored: HasVideo -> true ]
- Source reboot cycle detected             - Mark videoAliveTimestamp = now
- Clear inRecoveryWindow                   - Set inRecoveryWindow = true
- Clear all decoder blackout states        - Reset timeout reported flags
          │                                               │
          │                                               ▼
          │                                 ┌───────────────────────────┐
          │                                 │ For each decoder:         │
          │                                 │ - If displaying within    │
          │                                 │   <= 2.0s -> Record       │
          │                                 │   RecoveryTimeSec         │
          │                                 │ - If > 2.0s and still     │
          │                                 │   not displaying ->       │
          │                                 │   Flag POST_REBOOT_TIMEOUT│
          │                                 └─────────────┬─────────────┘
          │                                               │
          └───────────────────────┬───────────────────────┘
                                  ▼
                    [ Steady State (HasVideo == true) ]
                                  │
          ┌───────────────────────┴───────────────────────┐
          │                                               │
          ▼                                               ▼
[ Display drops: WasDisplaying -> false ]   [ Display restores: Blackout -> true ]
- Transition to IsCurrentlyBlackedOut = true - Calculate BlackoutDurationSec
- Record BlackoutStartTimestamp = now       - Record MIDSTREAM_RECOVERED event
- Flag MIDSTREAM_INTERMITTENT_DROPOUT event  - Restore WasDisplaying = true
- OverallStatus -> ANOMALY                   - OverallStatus -> PASS
```

### 7.1 Post-Reboot Recovery Window (2.0s Timeout)
- **Recovery Window Setting**: `AppSettings.AnomalyWindowSeconds = 2.0` (configurable in settings).
- **Trigger**: When composite encoder status transitions from `false -> true` (source reboot complete, video input restored).
- **Evaluation**: The engine tracks elapsed time `elapsedSinceAlive = (now - videoAliveTimestamp)`.
  - If a decoder recovers (`DisplayScreenStatus == "DISPLAYING_VIDEO"`), its `RecoveryTimeSec` is recorded.
  - If `elapsedSinceAlive > AnomalyWindowSeconds` (2.0s) and the decoder is still not displaying video:
    - Flag `IsAnomaly = true`
    - Set `OverallStatus = "ANOMALY"`
    - Set `FaultAttribution = "POST_REBOOT_RECOVERY_TIMEOUT"`
    - Fire `AnomalyDetected` event with `AnomalyEventType.POST_REBOOT_TIMEOUT`.
- **Window Closure**: Automatically closes once all monitored decoders are displaying video.

### 7.2 Intermittent Mid-Stream Blackout Tracking
- **Prerequisite**: Steady-state operation where the encoder continuously outputs video (`currentCompositeHasVideo == true`).
- **Dropout Detection**: If a decoder was previously active (`WasDisplaying == true`) and suddenly ceases to display (`DisplayScreenStatus != "DISPLAYING_VIDEO"`):
  - Mark `IsCurrentlyBlackedOut = true`
  - Record `BlackoutStartTimestamp = now`
  - Flag `IsAnomaly = true`
  - Fire `AnomalyDetected` event with `AnomalyEventType.MIDSTREAM_INTERMITTENT_DROPOUT`.
- **Recovery Detection**: If the display recovers before the source PC reboots:
  - Calculate `blackoutDuration = (now - BlackoutStartTimestamp)`.
  - Record `BlackoutDurationSec`.
  - Mark `IsCurrentlyBlackedOut = false`.
  - Fire `AnomalyDetected` event with `AnomalyEventType.MIDSTREAM_RECOVERED`.
- **Reboot Immunity**: When a normal source reboot cycle occurs (`currentCompositeHasVideo` transitions `true -> false`), all mid-stream blackout states are cleanly cleared so normal reboot outages are never misclassified as anomalies.

---

## 8. Multi-Tier Isolated CSV Logging System (`CsvLoggingService`)

### 8.1 Directory Isolation
Each test execution generates a completely isolated session folder under `logs/`:
```
logs/
└── Test_20260926_145021/
    ├── master_telemetry.csv
    ├── encoder_telemetry.csv
    ├── anomaly_events.csv
    ├── summary_report.csv
    └── decoders/
        ├── Decoder_Left_74fe488b22ca.csv
        └── Decoder_Right_74fe488b22cb.csv
```
All file write operations are serialized through a thread-safe `SemaphoreSlim(1, 1)` to eliminate I/O concurrency conflicts during high-frequency sampling.

### 8.2 File Schemas

#### 1. `master_telemetry.csv` (Chronological unified feed across all decoders)
```csv
Timestamp,DecoderName,MAC,IP,OverallStatus,FaultAttribution,SourceEncoderStatus,IsAnomaly,RecoveryTimeSec,BlackoutDurationSec,VideoReceivedStatus,DisplayScreenStatus,SourceEncoderMac,MulticastAddress,ClockStable,VideoRaster,PixelClockMhz,MultiLinkStatus,MultiLinkMode,NetworkPortSpeed,TemperatureC,DisplayModel,DisplaySerial,HpdConnected,TmdsActive,HdcpBlocked,StreamingActive,DisplayConnected,StreamPushingToDisplay,Notes
```

#### 2. `encoder_telemetry.csv` (Dedicated upstream transmitter telemetry)
```csv
Timestamp,EncoderName,MAC,IP,OverallStatus,SourceStable,HasVideo,VideoRaster,PixelClockMhz,StreamState,MulticastAddress,LinkMode,LinkStatus,Temperature,ErrorCode,Notes
```

#### 3. `anomaly_events.csv` (Incident ledger for automated alerting and analysis)
```csv
Timestamp,EventType,ElapsedSeconds,EncoderMAC,AffectedDecoderName,AffectedDecoderMAC,Description
```
*Event Types*: `POST_REBOOT_TIMEOUT`, `MIDSTREAM_INTERMITTENT_DROPOUT`, `MIDSTREAM_RECOVERED`.

#### 4. `decoders/<Name>_<MAC>.csv` (Per-decoder individual time-series log)
Contains all 27 decoder-specific columns for direct charting and spreadsheet pivot analysis.

#### 5. `summary_report.csv` (Final post-run session analytics)
```csv
SessionId,StartTime,EndTime,Duration,TotalSamples,PassCount,WarnCount,FailCount,MonitoredDecodersCount,DecoderMAC,DecoderName,SamplesCount,PassRatePercent,StreamingUptimePercent,DisplayUptimePercent,AverageRecoveryTimeSec
```

---

## 9. User Interface & Presentation Layer (WPF MVVM)

### 9.1 Visual Theme & Layout (`Themes/ModernTheme.xaml`)
- **Theme**: Dark Slate professional A/V engineering aesthetic (`#0F172A` background, `#1E293B` card surfaces, `#334155` borders).
- **Status Badges**:
  - Green (`#22C55E`): `PASS` / `VIDEO RECEIVED` / `DISPLAYING VIDEO`
  - Amber (`#EAB308`): `WARN` / `WAITING FOR SOURCE`
  - Red (`#EF4444`): `FAIL` / `NO VIDEO STREAM` / `NO CABLE`
  - Purple (`#A855F7`): `ANOMALY` / `POST_REBOOT_TIMEOUT` / `MIDSTREAM_BLACKOUT`
  - Blue (`#3B82F6`): Connected / Information

### 9.2 Interface Components
1. **Control Server Drawer**:
   - Inputs for Control Server IP, Port, and Polling Interval.
   - "Save & Verify" button triggering immediate socket and discovery probe with latency display (e.g., `Connected (12ms)`).
2. **Live KPI Metric Cards**:
   - Total Monitored Decoders, Elapsed Runtime Stopwatch, Live PASS count, Live WARN count, Live FAIL count, and Live **ANOMALIES** count badge.
3. **Upstream TX Pipeline Status Card**:
   - Displays real-time upstream transmitter status: Device Name, IP, MAC, Inbound Raster (`3840x2160@60Hz`), Pixel Clock (`594.0 MHz`), Stream Address (`224.1.1.200`), ASIC Temperature, and overall TX health.
4. **Interactive Decoder Inventory & Live DataGrid**:
   - Checkbox multi-select with "Select All" and "Clear" controls.
   - Dynamic search filter (matches MAC, Name, IP, or Status).
   - Columns: Select, Name, MAC, IP, Temp, Overall Status, **Fault Attribution**, **Recovery & Anomaly**, Video Received (Q1), Displaying Video (Q2), Raster, Display Model.
5. **Hardware Diagnostic Inspector Drawer**:
   - Expands detailed physical telemetry for the highlighted decoder: EDID Model Name, Serial Number, Native Timing, Audio Support, TMDS Clock State, HPD Pin Status, HDCP Protection, Network Port Speed, and Color Generator state.
6. **Real-Time Activity Log**:
   - Color-coded scrolling event ledger recording discovery events, state transitions, anomaly alerts, and summary stats.

---

## 10. Configuration & Settings Persistence (`SettingsService`)

The application configuration is managed via `config/settings.json`:

```json
{
  "control_server_ip": "127.0.0.1",
  "control_server_port": 8090,
  "polling_interval_seconds": 1.0,
  "timeout_ms": 3000,
  "duration_minutes": 0,
  "anomaly_window_seconds": 2.0,
  "selected_decoder_macs": [
    "74:fe:48:8b:22:ca",
    "74:fe:48:8b:22:cb"
  ]
}
```

- **Default Fallback**: Automatically copies from `config/config.default.json` if `settings.json` is missing.
- **Auto-Persistence**: Saves whenever server parameters are verified or decoders are selected in the UI.

---

## 11. Automated Test Suite & Quality Assurance

The codebase includes an extensive automated test suite built on xUnit, Moq, and FluentAssertions (`tests/AVASDecoderChecker.Tests`):

| Test Fixture | Purpose | Test Count |
|---|---|---|
| `AnomalyDetectorTests.cs` | Validates strict 2.0s post-reboot recovery window, mid-stream blackout detection, blackout duration calculation, recovery before reboot, and reset on reboot cycle. | 5 |
| `EncoderEvaluationTests.cs` | Validates TX telemetry extraction, single-link stability, dual-link companion pairing, composite stability logic, and error code handling. | 5 |
| `HealthEvaluatorOverhaulTests.cs` | Validates comprehensive fault attribution matrix (`SOURCE_REBOOTING`, `DECODER_PLL_DESYNC`, `DISPLAY_HPD_DOWN`, `DISPLAY_EDID_CORRUPT`, `DISPLAY_HDCP_BLOCKED`). | 5 |
| `TelemetryEvaluationTests.cs` | Validates legacy and JSON-based decoder telemetry evaluation, question badges, and thermal warnings. | 6 |
| `EdidParserTests.cs` | Validates VESA EDID 128-byte hex decoding, checksum verification, manufacturer name unpacking, model name descriptor, and serial numbers. | 4 |
| `CsvLoggingServiceTests.cs` | Validates isolated run directory creation, header formatting, multi-sample appending, and `summary_report.csv` statistics generation. | 4 |
| `MonitorEngineTests.cs` | Validates start/stop lifecycle, periodic polling loop execution, cancellation tokens, and event dispatching. | 4 |
| `MainViewModelTests.cs` | Validates MVVM command bindings, decoder selection persistence, UI property change notifications, and timer execution. | 4 |
| `SettingsServiceTests.cs` | Validates JSON deserialization, missing file defaults, and file write persistence. | 4 |
| **Total Automated Tests** | **All passing (0 failures, 0 skipped)** | **41** |

---

## 12. Deployment, Distribution & Git Integration

### 12.1 Target Runtime Environment
- **OS**: Windows 10 (1809+) / Windows 11 x64
- **Runtime**: Microsoft .NET 8.0 Desktop Runtime (`Microsoft.WindowsDesktop.App 8.0.x`)
- **Native Dependencies**: `VOIPS.dll` and companion AVoIP C-runtime libraries in application root.

### 12.2 GitHub Repository
- **Remote URL**: `https://github.com/TheMarkL-Corp/AVAS-Decoder-Checker`
- **Default Branch**: `main`
- **Release Version**: `v1.0.2`
- **Release Assets**:
  - `AVAS-Decoder-Checker-v1.0.2.zip` (Complete release package)
  - `AVAS-Decoder-Checker.zip` (Latest alias package)

### 12.3 Launch Scripts
- **PowerShell**: `scripts/run-app.ps1`
- **Batch**: `scripts/run-app.bat`
- **Dotnet CLI**: `dotnet run --project src/AVASDecoderChecker -c Release`
