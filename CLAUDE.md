# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

SNA is an open-source Discrete Event Simulation (DES) library for .NET, using the event-oriented formalism (events scheduled on a Future Event List, executed in timestamp order). It includes OpenTelemetry-based observability (traces, metrics, logs). See [README.md](README.md) for core concepts.

| Project | Purpose |
|---|---|
| `SimNextgenApp/` | The library. The only project packed and published to NuGet. |
| `SimNextgenApp.Tests/` | xUnit v3 tests on Microsoft.Testing.Platform (MTP), with Moq and `FakeTimeProvider`. |
| `SimNextgenApp.Demo/` | Console demos built on `System.CommandLine` 2.0 (`demo <subcommand>`), plus AWS RDS / Azure DB sample scripts. |
| `SimNextgenApp.Benchmarks/` | BenchmarkDotNet suite for engine and telemetry overhead. Built by PR CI but never run there. |

Target framework is `net10.0` with nullable reference types enabled. `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`.

## Commands

```bash
dotnet build                  # whole solution
dotnet test                   # MTP runner, configured in global.json
dotnet run --project SimNextgenApp.Demo -- demo --help
dotnet run --project SimNextgenApp.Demo -- demo mmck --servers 3 --capacity 10
dotnet run -c Release --project SimNextgenApp.Benchmarks -- --filter '*'   # results in BenchmarkDotNet.Artifacts/ (gitignored)
```

Sample scripts in `SimNextgenApp.Demo/AwsRdsSample/` and `SimNextgenApp.Demo/AzureDbSample/` come in `.sh` / `.ps1` pairs. They `cd` into their own directory first so they work from anywhere, and they plot results with `graph-cli` when it is installed. Keep the pairs in sync, and keep new scripts consistent with that pattern.

## Conventions

- **Issues first**: work starts from a GitHub issue.
- **Branches**: `<issue#>_Title-In-Kebab-Case`, e.g. `85_Fix-Docker-Build-and-Add-Dependabot`, branched from the latest `main`.
- **Commits**: `type(scope): description`, e.g. `fix(SimulationTelemetry): ...`. Types in use: `fix`, `feat`, `refactor`, `chore`.
- **PRs**: target `main` and include `Closes #<issue>`. PR CI (`.github/workflows/dotnet.yml`) runs restore, build and test only.
- **No ticket numbers in code**: comments explain the reason themselves instead of pointing to `#123` or a document outside the repo. Issue links belong in branch names, commit messages and PRs. The one exception is a `TODO` or workaround that links to an *open* issue tracking its removal.

## Releases

- Publishing a GitHub release with a `vX.Y.Z` tag on `main` triggers `.github/workflows/dotnet-release.yml`. It packs `SimNextgenApp` (version comes from the tag, not the csproj), publishes it to GitHub Packages, then builds and pushes the Demo image (`sna-demo`) to Docker Hub.
- Follow SemVer based on the library's changes since the last tag (`git diff vX.Y.Z..main -- SimNextgenApp/`). Changes to Demo, Tests, scripts or CI alone do not change the library version.
- The NuGet push does not use `--skip-duplicate`, so a tag cannot be re-run for an already-published version. Fix forward with a new patch release instead of moving tags.
- The Docker image is only built at release time, so Dockerfile problems surface there, not in PR CI.

## Dependencies

- Dependabot (`.github/dependabot.yml`) opens weekly PRs for NuGet, Docker and GitHub Actions. NuGet minor/patch updates are grouped; major updates get separate PRs.
- Prerelease packages can break in any version. `OpenTelemetry.Exporter.Prometheus.HttpListener` is a beta package: 1.19 replaced `UriPrefixes` with `Host` / `Port`. Check changelogs before merging.
- The Dockerfile uses floating `sdk:10.0` / `runtime:10.0` tags so each release build picks up the latest patches. A Dependabot PR for a new major image (e.g. `11.0`) needs a matching `TargetFramework` upgrade and must not be merged on its own.
