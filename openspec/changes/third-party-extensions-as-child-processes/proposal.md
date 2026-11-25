## Why

Others should be able to add sources, parsers, views and tools without changing the app. The plan makes each extension an MCP server in its own process with an MCP App view, which needs the spike (#wi1) result and the registry (#wi2).

## What Changes

- A manifest per extension; its views and settings show without starting it.
- A supervisor that runs extensions as child processes, restarts them and disables them if they keep crashing.
- Extension tools offered to agents, and extension views shown in a sandboxed frame.
- Extensions are off until the user turns them on; `--no-extensions` starts the app without them.
