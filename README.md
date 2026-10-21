# win-update-report

Collect local Windows Update history, failure codes, and reboot indicators.

A read-only C# / .NET 10 CLI for Windows 11 and Windows Server 2022/2025. No third-party runtime packages. Windows PowerShell 5.1 and the listed built-in cmdlets are required for live collection.

## Run

Download `win-update-report-win-x64` from a successful [GitHub Actions run](https://github.com/b69ca/win-update-report/actions), extract the artifact, and run the executable from a terminal. The standalone artifact includes its .NET runtime. These initial binaries are unsigned; verify their source and CI provenance according to your normal software policy.

```powershell
.\win-update-report.exe --days 14
.\win-update-report.exe --days 14 --json > report.json
.\win-update-report.exe --input report.json --json
```

Or build from source with the [.NET 10 SDK](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview):

```powershell
dotnet run --project WinUpdateReport.csproj -- --days 14
dotnet publish WinUpdateReport.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

The JSON output contains the evidence and can be replayed with `--input` on Windows or Linux. Live collection is Windows-only at the CLI boundary. A successful report can contain warnings or errors describing the diagnosed system; it is not a clean bill of health.

## Options

```text
--days N (1–30, default 14)
--max-history N (1–500, default 100)
--json          Full versioned JSON report
--input FILE    Analyze saved evidence without live collection
--help          Usage
```

`--input` cannot be combined with live collection options. Unknown and duplicate flags are rejected. Snapshots must match this tool and schema version 1, contain evidence and issues, and be no larger than 8 MiB. `--help` performs no collection. Ctrl-C cancels an active collection.

Exit codes: **0** report produced, including findings or partial evidence; **2** argument/snapshot error; **3** collection failed; **4** live collection attempted outside Windows; **130** cancelled. Automations must inspect `status`, `issues`, and finding severity rather than treating exit 0 as healthy. CLI errors go to stderr; `--json` runtime collection failures emit a `status: failed` envelope.

## Evidence and findings

Reads the local Windows Update Agent COM history using GetTotalHistoryCount/QueryHistory, recent WindowsUpdateClient System events, system-drive free space, and CBS/Windows Update/pending-file-operation registry indicators. It reads only the newest capped history entries and then filters them by date.

Failed, aborted, and succeeded-with-errors operations are reported with their HRESULTs. A historical failure may have succeeded on retry. HRESULT explanations describe the recorded code rather than guessing an underlying root cause.

It does not call the update search API, scan for updates, contact update servers to check availability, download, install, clear caches, run DISM/SFC, or repair components. History may be reset or incomplete and need not represent current installed state. Reboot markers can be stale. Pending file operations can come from unrelated software. Services or COM components may be unavailable on managed/minimal Windows configurations.

Text reports show the main facts and actionable findings. JSON includes `schemaVersion`, `tool`, `version`, `collectedAtUtc`, `analyzedAtUtc`, `mode`, `platform`, `status`, `evidence`, `issues`, and `findings`. Replaying retains the original collection timestamp/platform when recorded. Finding IDs are stable within this initial schema. Issues and bounds are preserved, so unavailable data is not silently presented as a successful check.

## Permissions and operational behavior

Start as a normal user. Some service, event-log, process-path, registry, and filesystem evidence can require additional privileges; failures are reported as collection issues. Elevation may improve coverage but does not guarantee access to every protected resource.

The tool does not terminate processes, change services, modify PATH/hosts/registry/firewall, delete files, install updates, or restart the computer. Network tools make the explicitly requested DNS/TCP/TLS probes. Reports can contain local paths, account names, hosts, process/service names, event messages, and public certificate metadata; review a report before sharing it. There is no telemetry or report-upload feature.

## Test and develop

```powershell
dotnet build WinUpdateReport.csproj -c Release
dotnet run --project tests/Checks.csproj -c Release
dotnet run --project WinUpdateReport.csproj -- --input tests/fixture.json --json
```

The dependency-free test executable uses assertions and exits nonzero on failure. Run it with `dotnet run`, not `dotnet test`. Fixtures are synthetic and use reserved example addresses/names. CI builds and runs fixtures on Linux and Windows; Windows also exercises the live collector where applicable and publishes a standalone x64 executable. TLS tests use isolated loopback endpoints, reject an untrusted certificate, and check timeout behavior. Disk tests use temporary directories and check reparse-point skipping where link creation is permitted.

Each repository is independent. `Core.cs` provides strict option parsing, snapshot replay, reporting, cancellation, and a bounded collector launcher. `Tool.cs` owns the diagnostic rules and native collection. Where present, `Collector.ps1` is embedded into the executable, uses built-in Windows inventory commands, and receives validated parameters through environment variables rather than interpolating user input into script code. Its subprocess is capped at 90 seconds and 8 MiB per output stream. Execution policy is not changed or bypassed.

This is a focused initial release. CI smoke tests establish that collectors execute on the hosted Windows runner, not that every Windows policy, locale, domain, or protected application is covered. Findings identify evidence and next checks; configuration changes remain a separate administrative decision.

## License

MIT
