# Configuration Guide

The configuration defines how the checker communicates with the SDVoE Control Server and which decoders to monitor.

### Parameters:

- **`control_server`**:
  - `ip`: IP address of the SDVoE Control Server host.
  - `port`: Control port (typically TCP `6970` for SDVoE API/Telnet or HTTP/WebSocket port).
  - `timeout_ms`: Socket or request timeout in milliseconds.

- **`decoders`**:
  - Array of Decoder definitions.
  - `device_id`: Hardware MAC address or unique identifier assigned by SDVoE (e.g. `00:0b:90:xx:xx:xx` or UUID).
  - `name`: Friendly human-readable label.
  - `hdmi_index`: Index of the HDMI output interface (default `0`).

- **`monitoring`**:
  - `interval_seconds`: Cadence of status checks.
  - `alert_on_*`: Flags to control alert conditions.
