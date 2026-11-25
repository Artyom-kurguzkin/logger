## Why

The app shows no logs yet. This is the first feature built end to end on the skeleton from #wi2: records are stored, a file is tailed into the store, and the user sees the lines in a fast list.

## What Changes

- A `LogStore` module on SQLite with full-text search.
- A file source that follows a file, and a JSON-lines parser.
- A log list that stays fast with very many lines.
