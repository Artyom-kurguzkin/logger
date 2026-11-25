## Why

`local/reqs.md` asks for OpenTelemetry traces, a trace explorer, graphs, queries and alerts. They all build on the log store from #wi3, so they wait for it.

## What Changes

- An OTLP receiver for logs and traces, a trace explorer, SQL queries, graphs of query results, and alerts on saved queries.
- This item is coarse: split it into separate items (receiver, traces, queries and graphs, alerts) when it is started.
