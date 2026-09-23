# AVAS Decoder (RX) HDMI Stream Checker

An automated diagnostic and health monitoring tool for AVAS / SDVoE Decoder (RX) HDMI streams.

## Overview

The **AVAS Decoder HDMI Stream Checker** interacts with an SDVoE Control Server to monitor and evaluate the real-time operational status of one or more Decoders (RX units). It specifically interrogates the control server to verify:

- **`streaming`**: Ensures video packets are actively flowing to the decoder.
- **`stable`**: Checks for clock stability and uninterrupted synchronization.
- **`HDMI_MONITOR.status.connected`**: Confirms physical display connection (Hot Plug Detect).
- **`subscriptions[HDMI:index].status.state`**: Assesses subscription routing status.

## Lifecycle Control

The checker is designed to operate under explicit control commands:
- **Start**: Initiates the background or interactive query loop against the SDVoE Control Server for all configured Decoder IDs.
- **Stop**: Gracefully halts querying and flushes diagnostic summaries.

## Project Structure

```
AVAS-Decoder-Checker/
├── PROJECT_MEMORY.md       # Persistent workspace memory & state tracking
├── README.md               # Project documentation
├── .gitignore              # Ignored files (logs, local configs, temp data)
├── config/
│   ├── config.default.json # Base configuration template
│   └── README.md           # Configuration documentation
├── docs/
│   ├── ARCHITECTURE.md     # Architecture and component design
│   └── SDVOE_REFERENCE.md  # SDVoE API and device reference notes
├── src/                    # Source code for the checker
├── scripts/                # Execution & control scripts
└── logs/                   # Diagnostic session logs
```

## Quick Start (Preview)

1. Review and populate your target configuration in `config/config.default.json` or through agent instructions.
2. Specify:
   - SDVoE Control Server IP & Port
   - One or more Decoder Device IDs
3. Start the checker on demand.

For project state and background context, see [`PROJECT_MEMORY.md`](PROJECT_MEMORY.md).
