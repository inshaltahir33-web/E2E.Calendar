# Packaging and updates

## Local MSIX builds

The packaging script publishes the self-contained WinUI application and creates
an unsigned MSIX with the Windows SDK `makeappx.exe` tool:

```powershell
.\packaging\Build-MSIX.ps1 -Architecture x64 -Configuration Release
.\packaging\Build-MSIX.ps1 -Architecture ARM64 -Configuration Release
```

Artifacts are written to `artifacts\msix\<architecture>`. The package is
unsigned by design so that Store signing or an approved sideload certificate
can be applied by the release pipeline. The manifest uses the `E2E Dev`
publisher identity, `internetClient`, and the restricted `runFullTrust`
capability required by the WinUI desktop executable.

## Update checks

`UpdateCheckService` runs in the hosted background service collection. It
performs an initial non-blocking check and repeats every six hours. Configure
the HTTPS manifest endpoint with either:

```text
AppUpdates:ManifestUrl=https://updates.example.com/e2e-calendar.json
E2E_UPDATE_MANIFEST_URL=https://updates.example.com/e2e-calendar.json
```

The endpoint returns a small JSON document:

```json
{
  "version": "1.0.1.0",
  "downloadUrl": "https://updates.example.com/e2e-calendar-1.0.1.msix"
}
```

Update failures are logged and never block startup or calendar interactions.
The app does not download or install updates automatically.
