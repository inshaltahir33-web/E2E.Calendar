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

## Image assets

`packaging\Assets` holds the single SVG placeholder design plus every PNG the
manifest is allowed to reference. MSIX manifests only accept PNG/JPEG, so the
PNGs are rasterised from the SVG design by:

```powershell
.\packaging\Generate-Assets.ps1
```

`Build-MSIX.ps1` calls this automatically and then fails the build if the
manifest references an image that is not in the package. Generated sizes:

| Asset | Size |
|---|---|
| `StoreLogo.png` | 50x50 |
| `Square44x44Logo.png` (+ `targetsize-{16,24,32,48,256}`, `altform-unplated_*`) | 44x44 |
| `Square71x71Logo.png` | 71x71 |
| `Square150x150Logo.png` (+ `scale-200`) | 150x150 |
| `Square310x310Logo.png` (+ `scale-200`) | 310x310 |
| `Wide310x150Logo.png` (+ `scale-200`) | 310x150 |
| `SplashScreen.png` | 620x300 |

## Before Store submission

The manifest in this repo is Store-shaped but not yet Store-bound:

- `Identity Name`/`Publisher` and `PublisherDisplayName` are the `E2E Dev`
  placeholders. Replace them with the values from Partner Center → Product
  identity (they must match the signing certificate subject exactly).
- The package must be signed with the Partner Center certificate (or uploaded as
  `.msixupload` via the Store pipeline); `Build-MSIX.ps1` emits an unsigned
  `.msix`.
- `dotnet build` uses `WindowsPackageType=None`, so the manifest is only applied
  by `Build-MSIX.ps1`; WACK (`appcert.exe`) is not run locally or in CI yet.
- Only `en-us` is declared in `<Resources>`; add `Resource` entries (and
  qualified assets) before shipping additional languages.


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
