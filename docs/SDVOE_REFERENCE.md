# SDVoE Reference & Knowledge Base

This document records technical details, API bindings, and architectural references from the official documentation and SDK in `AVoIP SDK and SDVoE Reference/`.

---

## 1. SDVoE Control Server Architecture & Ports

The SDVoE Control Server manages device discovery, routing, and telemetry reporting.

### Network Communication Interfaces:
- **TCP Text Command (CLI / Telnet)**: Default port `6970`
  - Bi-directional text command socket interface.
- **REST API (HTTP / HTTPS)**: Default port `80` (HTTP) or `443` (TLS)
  - RESTful JSON endpoints for querying device configuration, operational status, and active routing.
- **Underlying SDK (`VOIPS.dll`)**:
  - Found in `AVoIP SDK and SDVoE Reference/VOIPS SDK_20240607_V2.30/VOIPS SDK_20240607_V2.30/DLL/`
  - Targets both `.NET Standard 2.0` (runs natively on .NET 8 on this system) and `.NET Framework 4.5.2`.
  - Namespace: `VOIPS_LIB`, Primary Class: `VOIPS`.
  - Key methods:
    - `SET_CONTROL_SERVER(string address, int port)`
    - `SET_HTTP_PORT(int port)`
    - `GET_ALL_SETTINGS(string mac)`
    - `GET_REQUEST(int requestId)`
    - `QueryEvent(string type, int afterId, int limit)`
    - `GET_DEVICE_LIST()`

---

## 2. Target Telemetry Fields Specification

The HDMI health checker evaluates four critical indicators per Decoder (RX):

### 1. `streaming`
- **Location**:
  - `DEVICE_OBJECT.streams[index].status.state`
  - And `DEVICE_OBJECT.configuration.resume_streaming`
- **Healthy Criteria**:
  - State indicates active streaming transmission (e.g., `"STREAMING"`, `"STARTED"`, or `true`).

### 2. `stable`
- **Location**:
  - `HDMI_DECODER_NODE_STATUS.source_stable` (within `DEVICE_OBJECT.nodes` where node type is `HDMI_DECODER`)
- **Healthy Criteria**:
  - Value is `true`, confirming clock recovery and horizontal/vertical lock with source video without frame drops or sync slips.

### 3. `HDMI_MONITOR.status.connected`
- **Location**:
  - `HDMI_MONITOR_NODE_STATUS.connected` (within `DEVICE_OBJECT.nodes` where node type is `HDMI_MONITOR`)
- **Healthy Criteria**:
  - Value is `true`, confirming Hot Plug Detect (HPD) is asserted and an active HDMI display/sink is attached.

### 4. `subscriptions[HDMI:index].status.state`
- **Location**:
  - In `DEVICE_OBJECT.subscriptions` array, matching element where `type == "HDMI"` (or stream type HDMI) and `index == target_index`.
- **Healthy Criteria**:
  - `status.state` is active/connected (e.g., `"CONNECTED"`, `"SUBSCRIBED"`, or `"ACTIVE"`).

---

## 3. Discovered Reference Documents

The following documentation is stored in `AVoIP SDK and SDVoE Reference/`:
- `PDS-062489_SDVoE_Developers_API_Reference_Guide_rev3-9-0-0.pdf`: Full API command dictionary and schema.
- `PDS-062488_Developers_Introduction_to_SDVoE_API_User_ Guide_rev1p3.pdf`: Introduction to architecture and sessions.
- `PDS-062491_SDVoE_Developers_API_Application_Note_Library_rev2p9.pdf`: Application integration examples.
- `PDS-061874_BlueRiver_AV_Processor_Data_Sheet_Rev9.pdf`: BlueRiver AVP hardware capabilities.
- `PDS-062350_SDVoE_Control_Server_and_API_Release_Notes_rev3p9p0p1.pdf`: Control server release notes.
- `VOIPS SDK_20240607_V2.30`: Advantech VOIPS SDK with sample application and compiled libraries.
