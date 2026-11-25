## Why

The plan builds third-party extensions on MCP servers with MCP App views. Before building on it, we must prove the pieces work together in a small throwaway host: launching a stdio server, a custom request, and a sandboxed view.

## What Changes

- A minimal Photino.Blazor host and one stdio MCP extension, in `spike/`, thrown away afterwards.
- The result decides how steps 2 to 5 of the plan are built; no product behaviour changes.
