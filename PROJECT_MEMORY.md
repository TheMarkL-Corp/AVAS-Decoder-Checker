# PROJECT MEMORY: AVAS Decoder (RX) HDMI Stream Checker

> **Persistent Workspace Memory**  
> *Last Updated:* 2026-09-23  
> *Repository:* `AVAS-Decoder-Checker`  
> *Workspace Root:* `c:\Users\POC-615\Documents\GitHub\AVAS-Decoder-Checker`

---

## 1. Project Overview & Goal
This project is an automated **Decoder (RX) HDMI Stream Checker** tailored for AVAS / SDVoE hardware environments.

### Operational Workflow:
1. **Target Parameters**:
   - SDVoE Control Server IP & Port (provided by user, default Telnet `6970` / HTTP `80`).
   - Decoder Device IDs (MAC addresses or identifiers, 1 or more).
2. **Execution Commands**:
   - **START**: Initiates background or interactive polling and telemetry analysis.
   - **STOP**: Halts the checker process gracefully.
3. **HDMI Stream Health Assessment**:
   The checker queries the SDVoE Control Server and inspects 4 vital health criteria:
   - `streaming`: Video stream transmission active.
   - `stable`: Video clock sync lock stable (`HDMI_DECODER_NODE_STATUS.source_stable == true`).
   - `HDMI_MONITOR.status.connected`: Monitor Hot Plug Detect asserted (`HDMI_MONITOR_NODE_STATUS.connected == true`).
   - `subscriptions[HDMI:index].status.state`: Route subscription state between source and decoder HDMI output.

---

## 2. Health & Verification Metrics Breakdown

| Metric / Path | Target Object in SDVoE API / VOIPS SDK | Target Condition | Diagnostic Meaning |
|---|---|---|---|
| `streaming` | `DEVICE_OBJECT.streams[index].status.state` / `configuration.resume_streaming` | `"STREAMING"` / `"STARTED"` / `true` | Video transmission packets active |
| `stable` | `HDMI_DECODER_NODE_STATUS.source_stable` (under `nodes`) | `true` | Video clock synchronization locked, no jitter/drop |
| `HDMI_MONITOR.status.connected` | `HDMI_MONITOR_NODE_STATUS.connected` (under `nodes`) | `true` | Display monitor physically connected (HPD high) |
| `subscriptions[HDMI:index].status.state` | `DEVICE_OBJECT.subscriptions` (`type == "HDMI"`, matching `index`) | `"CONNECTED"` / `"SUBSCRIBED"` / `"ACTIVE"` | Video route successfully subscribed from transmitter |

---

## 3. Configuration & Parameters State

### SDVoE Control Server
- **Server IP**: *[Awaiting User Input]*
- **Server Port**: *[Awaiting User Input]* (Defaults: `6970` for Telnet / `80` for HTTP REST)
- **SDK / Protocol**: `VOIPS.dll` (.NET Standard 2.0 / .NET 8) or direct REST / TCP socket

### Target Devices (Decoders / RX)
- **Device IDs**: *[Awaiting User Input - supports 1 or more device IDs]*

### Checker Process State
- **Process Status**: `IDLE` (Options: `IDLE`, `RUNNING`, `STOPPED`, `ERROR`)
- **Last Started**: N/A
- **Last Stopped**: N/A
- **Last Polled**: N/A

---

## 4. Knowledge Base & Reference Registry
Reference documents and SDK located in [`AVoIP SDK and SDVoE Reference/`](AVoIP%20SDK%20and%20SDVoE%20Reference/):
- **API Reference**: `PDS-062489_SDVoE_Developers_API_Reference_Guide_rev3-9-0-0.pdf`
- **User Guide**: `PDS-062488_Developers_Introduction_to_SDVoE_API_User_ Guide_rev1p3.pdf`
- **Application Note**: `PDS-062491_SDVoE_Developers_API_Application_Note_Library_rev2p9.pdf`
- **Data Sheet**: `PDS-061874_BlueRiver_AV_Processor_Data_Sheet_Rev9.pdf`
- **SDK Library**: `VOIPS SDK_20240607_V2.30` containing `VOIPS.dll` and `controlserver.exe`

---

## 5. Architectural Principles & Implementation Plan
- **Engine**: Cross-platform C# (.NET 8.0) console / service utilizing `VOIPS.dll` (.NET Standard 2.0) or native HTTP/TCP clients, accompanied by PowerShell launch/control scripts.
- **Modules**:
  - `ConfigManager`: Loads target server IP, port, decoder list, thresholds from `config/config.json`.
  - `SDVoECheckerEngine`: Manages query cycles against SDVoE Control Server.
  - `HealthEvaluator`: Verifies `streaming`, `stable`, `HDMI_MONITOR.status.connected`, and `subscriptions[HDMI:index].status.state`.
  - `ProcessController`: Handles Start/Stop commands with clean thread management and non-blocking operation.
  - `DiagnosticLogger`: Formats live console output with status indicators (`[OK]`, `[FAIL]`, `[WARN]`) and persists timestamped logs to `logs/`.

---

## 6. Action Items & Current Status
- [x] Initialize Git repository (`main` branch) and configure `.gitignore`.
- [x] Establish consistent project memory (`PROJECT_MEMORY.md`).
- [x] Configure base project directory skeleton (`config/`, `docs/`, `src/`, `logs/`, `scripts/`).
- [x] Ingest and analyze reference knowledge from `AVoIP SDK and SDVoE Reference/`.
- [x] Map exact field definitions for `streaming`, `stable`, `HDMI_MONITOR`, and `subscriptions`.
- [ ] Receive SDVoE Control Server IP and Port from user.
- [ ] Receive Decoder (RX) Device IDs from user.
- [ ] Execute Start / Stop commands upon user request.
