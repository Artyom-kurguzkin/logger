## ADDED Requirements

### Requirement: An extension is described by a manifest
An extension SHALL be a folder with an `extension.json` that names it and lists the views, sources, parsers and alert actions it contributes, so they show without starting its process.

#### Scenario: Contributed view
- **WHEN** the manifest lists a view with a title and an icon
- **THEN** the side menu shows that view before the extension process has started

### Requirement: Extensions are disabled until enabled
A found extension MUST stay disabled until the user enables it in Settings, and Settings SHALL show its manifest, permissions and declared web domains first.

#### Scenario: New extension found
- **WHEN** a new extension folder appears
- **THEN** the extension is listed as disabled and none of its code runs

### Requirement: Extension tools are offered to agents with a prefix
The app SHALL offer each tool of an enabled extension to agents under the name made of the extension id, an underscore and the tool name.

#### Scenario: Prefixed tool name
- **WHEN** the extension `hello` has a tool `greet`
- **THEN** agents see a tool named `hello_greet` and calling it runs the extension's tool

### Requirement: Extension views run in a sandboxed frame
The app SHALL show an extension view in a sandboxed iframe on a different origin than the app, with a content security policy built from the domains the extension declared, and its tool calls MUST go only to that extension.

#### Scenario: View loads
- **WHEN** the user opens an extension view
- **THEN** the page appears in a sandboxed frame and cannot read the app's own page

#### Scenario: View calls another extension
- **WHEN** a view asks to call a tool of a different extension
- **THEN** the call is refused

### Requirement: A crashing extension is restarted, then disabled
The app SHALL restart an extension whose process ends unexpectedly, and MUST disable it and tell the user after 3 failures, without stopping the app.

#### Scenario: One crash
- **WHEN** an extension process ends unexpectedly once
- **THEN** it is started again

#### Scenario: Repeated crashes
- **WHEN** the extension fails 3 times
- **THEN** it is disabled, a message is shown and the app keeps running

### Requirement: Extensions can be turned off at start
The app SHALL start without loading any extension when run with `--no-extensions`.

#### Scenario: Flag given
- **WHEN** the app starts with `--no-extensions` and an enabled extension exists
- **THEN** no extension view, tool or process is present
