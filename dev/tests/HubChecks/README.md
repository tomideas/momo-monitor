# Hardware hub regression checks

```powershell
dotnet run --project dev/tests/HubChecks/HubChecks.csproj -c Release
```

The harness links the production `HardwareMonitorHub` against a test-only `Computer`
implementation. That source type shadows the library class, and its Open / Close methods
only update counters. All hardware nodes are interface proxies. No native driver or actual
hardware can be opened by this test.

Checks cover failed updates, hiding stale sensor trees, recovery, closing and reopening for
retry, failed opens, and disposal.
