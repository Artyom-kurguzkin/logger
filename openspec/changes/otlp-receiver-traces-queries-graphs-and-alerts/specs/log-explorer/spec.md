## ADDED Requirements

### Requirement: OTLP data is received
The app SHALL accept OpenTelemetry logs at `/v1/logs` and traces at `/v1/traces` and store them.

#### Scenario: Trace sent
- **WHEN** an application posts a trace to `/v1/traces`
- **THEN** its spans are stored and can be found in the trace explorer

### Requirement: Traces can be explored per feature or component
The trace explorer SHALL list traces and let the user filter them by feature or component.

#### Scenario: Filter by component
- **WHEN** the user filters traces by the component "billing"
- **THEN** only traces that touch "billing" are listed

### Requirement: Stored data can be queried
The app SHALL let the user run and save queries over the stored records and show the result.

#### Scenario: Saved query
- **WHEN** the user saves a query for errors and runs it
- **THEN** the matching records are listed

### Requirement: Query results can be graphed
The app SHALL draw a graph from the result of a query.

#### Scenario: Errors per minute
- **WHEN** the user graphs a query that counts errors per minute
- **THEN** a graph with one point per minute is shown

### Requirement: Alerts watch saved queries
The app SHALL run a saved query on a timer and fire an alert when its result is over the set threshold.

#### Scenario: Threshold passed
- **WHEN** the error count of an alert's query goes over its threshold
- **THEN** the alert fires once and shows a message
