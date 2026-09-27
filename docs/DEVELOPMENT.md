# BSManager development, build and release protocol

## Branches

| Branch | Purpose | Who writes to it |
|---|---|---|
| `master` | Released code. Every push that passes CI publishes release `v<Version>` (once per version). Installed copies read the auto-updater file from here. | Merge from `dev` (release) or `hotfix/*` only |
| `dev` | Integration branch. Every push that passes CI publishes a pre-release `v<next>-dev.<run>`. | Pull requests from topic branches |
| `feat/*`, `fix/*`, `ci/*`, `claude/*` | One topic each (a feature, one bug, CI work, a Claude session) | The author |
| `hotfix/*` | Urgent fix for the released version, branched from `master` | The author |

GitHub settings (Settings → Rules → Rulesets), set by the repository owner:
- `master` and `dev`: require a pull request, require the **CI** check `Build` to pass, no force-push, no deletion.
- Merge style: *squash* for topic → `dev`, *merge commit* for `dev` → `master` (and for `hotfix/*` → `master`, `master` → `dev`).

## Development cycle

1. Pick an item (issue or idea). Branch from `dev`: `git switch -c fix/bs-v2-wakeup origin/dev`.
2. Build and try the change locally (Visual Studio, or `msbuild BSManager.csproj -restore -p:Configuration=Release -p:Platform=x64`).
3. Open a PR into `dev`. CI must be green.
4. Update the **Changelog in README.md** under the entry of the next, unreleased version (never under an already-released version; never bump `<Version>` in a feature PR).
5. Squash-merge. The merge publishes a `dev` pre-release automatically.

## Build

BSManager is a Windows-only WinForms app (`netcoreapp3.1`, `win10-x64`). It has a COM reference (`IWshRuntimeLibrary`), so it builds with the Visual Studio MSBuild (`msbuild`), not with `dotnet build`.

Release build, as CI does it:

```powershell
msbuild BSManager.csproj -restore -t:Publish -p:Configuration=Release -p:Platform=x64 `
  -p:RuntimeIdentifier=win10-x64 -p:SelfContained=true -p:PublishSingleFile=true `
  -p:PublishReadyToRun=true -p:DebugType=None -p:PublishDir="$PWD\out\"
```

## CI

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | every push; PRs into `master`/`dev` | builds the self-contained single-file `BSManager.exe` on Windows and uploads it (with `BSManager.zip`) as the `BSManager` artifact of the run; on `dev`/`master` pushes also publishes (see *Releases*) |

## Releases

Publishing is automatic and gated on the `Build` job:

| Push to | Publishes | Tag |
|---|---|---|
| `dev` | **pre-release** | `v<next>-dev.<run number>`: `<next>` is `<Version>` of `BSManager.csproj` if that version is not released yet, else the next patch version. The 10 newest dev pre-releases are kept. |
| `master` | **release** (marked latest) | `v<Version>`, only if that release does not exist yet (otherwise nothing is published) |

Assets: `BSManager.zip` (the executable, zipped; this is what the auto-updater downloads) and `BSManager.exe`. Release notes are the `- v<Version>` entry of the README changelog.

### Auto-updater

Installed copies check `BSManager/AutoUpdaterBSManager.json` on `master` (raw.githubusercontent.com). After publishing a release, CI opens a PR into `master` that sets `version`, `url` and the SHA-256 `checksum` of the new `BSManager.zip`. Merging that PR is what offers the update to users (it publishes nothing, the version is already released). This needs Settings → Actions → General → *Allow GitHub Actions to create and approve pull requests*; without it CI pushes the branch `release/autoupdater-v<Version>` and warns, and the PR is opened by hand.

Releases (not dev pre-releases) are announced on Discord through the webhook URL stored in the repository secret `TECH_CORNER_DISCOWH`. A missing secret or a Discord error is reported as a warning and never fails the release.

Release steps:
1. On `dev`: all PRs for the release merged, CI green, a dev pre-release exists.
2. Owner bumps `<Version>` in `BSManager.csproj` and completes the `- v<Version>` changelog entry in README.md (PR into `dev`).
3. Try the latest dev pre-release with a headset and base stations: wake-up and sleep for v1 and v2 stations, HMD on/off detection, Manage Runtime, Run at Startup, tray menu.
4. PR `dev` → `master`, merge commit. CI publishes release `v<Version>` and opens the auto-updater PR.
5. Check the release, then merge the auto-updater PR.
6. Hotfix: branch `hotfix/*` from `master`, bump the patch version, PR into `master`, then merge `master` back into `dev`.
