# AVAS Decoder HDMI Stream Checker Architecture (v1.0.2)

> **Document Type:** System Architecture Reference  
> **Detailed Specification:** For the complete, exhaustive software specification covering all telemetry metrics, state machines, protocol behaviors, and schemas, refer to [`docs/SOFTWARE_SPECIFICATION.md`](file:///c:/Users/POC-615/Documents/GitHub/AVAS-Decoder-Checker/docs/SOFTWARE_SPECIFICATION.md).

---

## 1. System Block Diagram

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

## 2. Core Architectural Components

1. **Hardware Communication Layer (`SdvoeService`)**:
   - Manages connection to the SDVoE Control Server via unmanaged C# bridge `VOIPS.dll` and fallback HTTP REST client.
   - Automatically probes and negotiates server ports (auto-detects between `8090` and `80`).
   - Handles asynchronous `GET_ALL_SETTINGS` JSON-RPC polling with `GET_REQUEST` for large switch fabrics.

2. **Upstream Transmitter (TX) Telemetry Pipeline**:
   - Discovers and polls active upstream transmitters on the network (`identity.is_transmitter == true`).
   - Extracts incoming HDMI source lock (`HDMI_DECODER.source_stable`), pixel clock, frame rate, and raster.
   - Dynamically correlates dual-link companion transmitters (e.g. `74fe488b07bb` and `74fe488b07bc`), requiring composite video stability across both transmission chips for 4K video feeds.

3. **Decoder (RX) Telemetry & Evaluation Engine (`HealthEvaluator`)**:
   - **Question 1 (Inbound Stream)**: Evaluates route subscriptions, multicast streaming status, genlock clock stability, and input rasters.
   - **Question 2 (Outbound Display)**: Evaluates physical Hot-Plug Detect (HPD pin 19), 128-byte VESA EDID parsing, TMDS clock oscillation, and HDCP authentication status.
   - **Root-Cause Fault Attribution**: Methodically isolates failures into 10 discrete categories:
     - `NONE`, `SOURCE_REBOOTING`, `POST_REBOOT_RECOVERY_TIMEOUT`, `DECODER_PLL_DESYNC`, `DECODER_DUAL_DESYNC`, `DECODER_STREAM_LOSS`, `DISPLAY_HPD_DOWN`, `DISPLAY_EDID_CORRUPT`, `DISPLAY_HDCP_BLOCKED`, `SERVER_TIMEOUT`.

4. **Anomaly Detection State Machine (`AnomalyDetector`)**:
   - **2.0-Second Recovery Timeout**: Measures elapsed latency when the upstream video source reboots (`HasVideo: false -> true`). Flags `POST_REBOOT_TIMEOUT` if any decoder takes > 2.0s to display video.
   - **Intermittent Mid-Stream Blackout Tracking**: When the transmitter has continuous video, tracks sudden dropouts on previously active displays, measures exact blackout durations, records whether the display recovered before the next reboot (`MIDSTREAM_RECOVERED`), and logs incidents to `anomaly_events.csv`.
   - **Reboot Immunity**: Automatically clears blackout tracking on routine source reboots (`HasVideo: true -> false`).

5. **Multi-Tier CSV Logging Service (`CsvLoggingService`)**:
   - Creates an isolated run directory for every session: `logs/Test_YYYYMMDD_HHmmss/`.
   - Writes `master_telemetry.csv`, `encoder_telemetry.csv`, `anomaly_events.csv`, per-decoder files `decoders/<Name>_<MAC>.csv`, and `summary_report.csv`.
   - Protects file I/O using `SemaphoreSlim(1, 1)` concurrency locks.

6. **Reactive Presentation Layer (`MainViewModel` / `MainWindow`)**:
   - Built on WPF MVVM with a modern Dark Slate design system (`ModernTheme.xaml`).
   - Includes Upstream TX Pipeline Status Card, Live KPI Counters, Searchable Multi-Select Decoder Grid, Hardware Diagnostic Inspector, and Real-Time Activity Log.
