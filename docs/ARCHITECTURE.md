# AVAS Decoder HDMI Stream Checker Architecture

```
 +-------------------------------------------------------+
 |                 User / Operator Control               |
 |                   (START / STOP)                      |
 +-------------------------------------------------------+
                            |
                            v
       +-----------------------------------------+
       |         AVAS Stream Checker Core         |
       |  - Lifecycle Controller (Start/Stop)    |
       |  - Periodic Poll / Telemetry Dispatcher |
       +-----------------------------------------+
             |                              |
             | Queries                      | Logs / Reports
             v                              v
+------------------------+      +-------------------------+
|  SDVoE Control Server  |      |   Console & File Logs   |
|   (IP:Port, e.g. 6970) |      |   Status Snapshots      |
+------------------------+      +-------------------------+
             |
             | Telemetry Responses
             v
+---------------------------------------------------------+
|                  Evaluation Pipeline                    |
|                                                         |
|  1. "streaming"                             [OK / FAIL] |
|  2. "stable"                                [OK / FAIL] |
|  3. "HDMI_MONITOR.status.connected"         [OK / FAIL] |
|  4. "subscriptions[HDMI:index].status.state"[OK / FAIL] |
+---------------------------------------------------------+
```

## Component Breakdown

1. **Connection Layer**:
   Connects to SDVoE Control Server via socket or API client, managing command dispatch, timeout, and reconnects.

2. **Query & Ingestion Layer**:
   Requests device state, HDMI monitor status, subscription status, and clock stability metrics for target decoders.

3. **Evaluation & Rule Engine**:
   Compares raw telemetry attributes against expected healthy values. Produces unified health reports.

4. **Lifecycle Manager**:
   Maintains checker state machine (`IDLE` -> `RUNNING` -> `STOPPED`), allowing non-blocking starts and stops.
