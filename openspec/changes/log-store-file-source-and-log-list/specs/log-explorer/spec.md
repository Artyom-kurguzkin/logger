## ADDED Requirements

### Requirement: Records are stored and queried
The log store SHALL keep appended records and answer queries over them, including text search on the message.

#### Scenario: Append then query
- **WHEN** three records are appended and the user searches for a word that appears in one message
- **THEN** only that record is returned

### Requirement: The store announces changes
The log store SHALL notify listeners when records are appended.

#### Scenario: New record
- **WHEN** a record is appended
- **THEN** every listener is told that the store changed

### Requirement: A file source follows a file
A file source SHALL read the lines added to its file and append them as records to the store.

#### Scenario: Line added to the file
- **WHEN** a line is written at the end of the followed file
- **THEN** a new record with that line appears in the store

### Requirement: JSON lines are parsed
The JSON-lines parser SHALL turn one JSON object per line into a record, and MUST keep a line it cannot read as a plain-text record.

#### Scenario: Valid JSON line
- **WHEN** the line is `{"level":"error","message":"boom"}`
- **THEN** the record has level error and message boom

#### Scenario: Line that is not JSON
- **WHEN** the line is `not json`
- **THEN** a record with that text as its message is stored

### Requirement: The log list stays fast with many records
The log list SHALL draw only the rows that are visible, so that a very large store does not slow it.

#### Scenario: Large store
- **WHEN** the store holds one million records and the user opens the list
- **THEN** only the visible rows are drawn and scrolling stays smooth
