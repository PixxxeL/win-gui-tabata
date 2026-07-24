# ScheduleTimer — Technical Guide

[Русская версия](technical.ru.md)

WPF application targeting .NET 8 (`net8.0-windows`). Dependency:
[NAudio](https://github.com/naudio/NAudio) (WAV/MP3 playback).

## Project layout

The code lives in `src/`, assets (icon, ticking sound, images) — in `assets/`.
Version history is in `CHANGELOG.md`.

Release builds are produced by GitHub Actions on a `vX.Y.Z` tag
(see `.github/workflows/release.yml`): two zips — self-contained (runtime
included) and no-runtime (requires .NET 8 Desktop Runtime). The version is
taken from the tag name. MSIX packaging in CI is temporarily disabled.

## Regular build

Requires the .NET 8 SDK.

```cmd
dotnet build ScheduleTimer.sln -c Debug
```

## Analytics (AppMetrica)

AppMetrica keys are **baked into the binary at build time** from the
`APPMETRICA_POST_API_KEY` and `APPMETRICA_APP_ID` environment variables
(see `Analytics.cs`). The repository contains no values. If the variables are
not set at build time — analytics is disabled in that build and nothing is
sent over the network.

Building with analytics (cmd; `set` applies only to the current window,
the value goes without quotes and without spaces around `=`):

```cmd
set APPMETRICA_POST_API_KEY=<Post API key>
set APPMETRICA_APP_ID=<application id>

dotnet build ScheduleTimer.sln -c Release
```

It is convenient to keep these two `set` lines in a file **outside the
repository** (e.g. `E:\pro\secrets.cmd`) and run `call E:\pro\secrets.cmd`
before building.

## MSIX package

The `.wapproj` is built only by the "big" MSBuild from Visual Studio
(not `dotnet build`). In the same cmd window where the variables are set:

```cmd
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ScheduleTimer.sln /restore /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundle=Never
```

## Building from Visual Studio 2022

VS must see the environment variables at launch time. To avoid keeping secrets
in system variables, start VS from a cmd window with the variables set — they
live only inside that VS instance:

```cmd
call E:\pro\secrets.cmd
start "" "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" ScheduleTimer.sln
```

## Verifying that keys are baked into a build

The keys are stored as `AssemblyMetadata` assembly attributes in
`ScheduleTimer.dll`. You can inspect them with PowerShell 7 (`pwsh`; the
built-in PowerShell 5.1 cannot read net8 assemblies):

```cmd
pwsh -c "[Reflection.Assembly]::LoadFile('<path>\ScheduleTimer.dll').GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq 'AssemblyMetadataAttribute' } | ForEach-Object { \"$($_.ConstructorArguments[0].Value) = $($_.ConstructorArguments[1].Value)\" }"
```

Empty values — the keys are not baked in, analytics is disabled. An
alternative is to open the dll in
[ILSpy](https://github.com/icsharpcode/ILSpy). If the dll is inside an
`.msix`: it is a regular zip — rename a copy to `.zip` and extract it.

Runtime diagnostics: the application writes `analytics.log` to its data
directory — it contains the analytics status (ENABLED/DISABLED) and AppMetrica
server responses.
