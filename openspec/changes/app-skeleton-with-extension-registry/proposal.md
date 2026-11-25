## Why

ak-logger needs a window with a side menu and pages before any log feature exists. Views must come from a registry, not a fixed list, so built-in features and later third-party extensions plug in the same way.

## What Changes

- A solution, a host shell with the design system copied from the task manager, and a small SDK project.
- An extension registry that holds views; pages and the side menu are built from it.
