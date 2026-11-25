## 1. Manifest and settings

- [ ] 1.1 Read `extension.json` from the extensions folder and register its contributions <!-- agent: Claude -->
- [ ] 1.2 Add the Extensions section in Settings to see the manifest and enable or disable <!-- agent: Claude -->
- [ ] 1.3 Add the `--no-extensions` flag <!-- agent: Claude -->

## 2. Process supervisor

- [ ] 2.1 Start an extension on first use and talk MCP over stdio <!-- agent: Claude -->
- [ ] 2.2 Restart with a delay on a crash, and disable after 3 failures with a toast <!-- agent: Claude -->
- [ ] 2.3 End children with the host, and shut down by closing stdin, waiting, then killing <!-- agent: Claude -->

## 3. Tools and views

- [ ] 3.1 Offer extension tools to agents with the extension id as prefix <!-- agent: Claude -->
- [ ] 3.2 Add `ExtensionFrame`: a sandboxed iframe with the `postMessage` bridge <!-- agent: Claude -->

## 4. Docs and samples

- [ ] 4.1 Write `docs/extension-protocol.md` <!-- agent: Claude -->
- [ ] 4.2 Add the samples `HelloDotnet` and `hello-script` <!-- agent: Claude -->
