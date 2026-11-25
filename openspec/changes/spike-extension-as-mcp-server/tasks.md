## 1. Confirm it works

- [x] 1.1 The C# SDK client launches a stdio server and sends a custom `aklogger/*` request <!-- agent: Claude -->
- [x] 1.2 `server/discover` shows our extension key <!-- agent: Claude -->
- [x] 1.3 A `ui://` HTML resource renders in a sandboxed iframe from the loopback origin under Photino's CSP <!-- agent: Claude -->
- [x] 1.4 A `postMessage` round trip works, including a proxied `tools/call` <!-- agent: Claude -->

## 2. Decide

- [x] 2.1 If custom methods cannot be sent, switch to reserved `_aklogger_` tools hidden from agents <!-- agent: Claude -->
- [x] 2.2 Write the result down so the later items can follow it <!-- agent: Claude -->
