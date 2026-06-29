# McpForUnity Agent

Windows tray agent for running `mcp-for-unity` with a pinned server package and a small management UI.

## Build

Use VS Code `Run Build Task` or run:

```powershell
dotnet build .\McpForUnityAgent.csproj -c Release
```

The default VS Code build task is `Build McpForUnityAgent`.

To validate dependency setup without uninstalling anything from the current PC, run the VS Code task `Check Dependency Setup` or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-dependencies.ps1
```

This checks that the app can reach the official Astral `uv` installer, shows the latest `uv` release version, checks the latest Git for Windows release installer, and prints the installer actions used when a dependency is missing.

## Package

Run the VS Code task `Package McpForUnityAgent` or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

The package is written to `artifacts\McpForUnityAgent` and includes:

- `McpForUnityAgent.exe`
- `install.cmd`
- `uninstall.cmd`
- `README.md`
- `VERSION.txt`
- `src`

## Install

From the package folder, run `install.cmd`.

The installer can also check or install dependencies:

- `uv`
- `Git`

`uv` is installed through Astral's official installer script. If `uv` is already installed, the reinstall action runs `uv self update` first and falls back to the official installer if needed. `Git` is installed by downloading and launching the latest Git for Windows 64-bit installer from the official Git for Windows release feed.

The default server command is:

```powershell
%USERPROFILE%\.local\bin\uvx.exe --from "mcpforunityserver==9.7.3" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools
```

## Tray Menu

- `Start`, `Stop`, `Restart`
- `Console`
- `Ports`
- `Install Unity Plugin...`
- `Logs Folder`
- `Config`
- `About`

The installer and Config window both expose `Dependencies...`, which shows uv/Git status with per-dependency install or reinstall buttons.

Config also includes auto-restart settings. `Port cleanup` can kill the configured port before an automatic retry, which helps recover from port allocation failures instead of looping on the same occupied port.

`Install Unity Plugin...` opens a path-entry dialog. You can paste a Unity project root path directly, or use `Browse...`. The installer updates `Packages/manifest.json` and creates a timestamped backup before writing.
