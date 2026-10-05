# Avalonia 12 upgrade preparation: package and build inventory (2026-10)

## Scope and evidence

This document only covers preparation for the upgrade. According to the owner decision in `TODO.md`, Avalonia 11 → 12 is scheduled before `1.3.5`; the upgrade is not being performed now. The current main Avalonia package version is **11.3.12**; no 12.x version has been selected, and no packages, code, XAML, TODO or roadmap have been changed.

Inventory baseline: 2026-10-03, commit `7477b07d`, branch `feature/queue/avalonia12-prep-packages-build`. `AGENTS.md`, `docs/generated/project-dependency-graph.md`, `docs/agents/domain.md` and the 1.3.x roadmap were read first; the five projects in the dependency graph match the current solution／ProjectReference relationships.

Sources are the [central package versions](../../Directory.Packages.props), [shared build settings](../../Directory.Build.props), the five csproj files below, [SDK settings](../../global.json), [CI workflow](../../.github/workflows/ci.yml) and existing verification scripts. Transitive dependency versions were also checked against the `net8.0` target in the restored `build/obj/FreeformHelper.UI/project.assets.json` and `build/obj/FreeformHelper.Tests/project.assets.json`; these two files are local artifacts, not lock files. This work did not access the network, run restore or read private test data.

Avalonia 12 changes cannot be verified without network access. Every open item in this document concerning 12 compatibility, minimum versions or build requirements is marked **to check against the official migration guide**; local evidence for the current 11 version cannot be treated as a compatibility guarantee for 12.

## Central versions and direct references

`Directory.Build.props` sets `ManagePackageVersionsCentrally=true`. No PackageReference in any csproj specifies `Version` or `VersionOverride`; versions come from `Directory.Packages.props`.

| Package | Central version | Directly referencing project |
| --- | --- | --- |
| `Avalonia` | `11.3.12` | UI |
| `Avalonia.Desktop` | `11.3.12` | UI |
| `Avalonia.AvaloniaEdit` | `11.4.1` | UI |
| `Avalonia.Themes.Fluent` | `11.3.12` | UI |
| `Avalonia.Fonts.Inter` | `11.3.12` | UI |
| `Avalonia.Headless` | `11.3.12` | Tests |
| `Avalonia.Headless.XUnit` | `11.3.12` | Tests |

UI is [FreeformHelper.UI.csproj](../../src/FreeformHelper.UI/FreeformHelper.UI.csproj), and Tests is [FreeformHelper.Tests.csproj](../../tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj). Tests references UI and inherits its desktop, theme, font, editor and renderer dependencies.

The other three projects are [Domain](../../src/FreeformHelper.Domain/FreeformHelper.Domain.csproj), [Application](../../src/FreeformHelper.Application/FreeformHelper.Application.csproj) and [Infrastructure](../../src/FreeformHelper.Infrastructure/FreeformHelper.Infrastructure.csproj), with no direct Avalonia references. Application's only direct package is `Clipper2 2.0.0`; Infrastructure references Application／Domain and has no package references of its own.

| Direct dependency to check together | Central version／reference | Items to check during the upgrade |
| --- | --- | --- |
| `CommunityToolkit.Mvvm` | `8.3.2`／UI | Compatibility of the existing MVVM source generator with the new toolchain: to check against the official migration guide. There is no evidence that it needs to change to the same version as Avalonia. |
| `System.Reactive` | `6.0.0`／UI | Whether the new Avalonia package graph still accepts the current version: to check against the official migration guide. |
| `Microsoft.NET.Test.Sdk` | `17.11.1`／Tests | Version required by the new Headless test toolchain: to check against the official migration guide. |
| `xunit` | `2.9.2`／Tests | Test framework compatibility of `Avalonia.Headless.XUnit`: to check against the official migration guide. |
| `xunit.runner.visualstudio` | `3.0.0`／Tests | test discovery and runner compatibility: to check against the official migration guide. |

UI also directly references `NLog 5.3.4` and `NLog.Extensions.Logging 5.3.4`; the current dependency graph provides no evidence that they need to be upgraded along with Avalonia.

The main package, Desktop, Fluent, Inter, Headless and Headless.XUnit currently share the same version. These references and restore results should be checked together in the future to avoid incompatible Avalonia assemblies in production and headless; package combinations and version alignment rules for 12: to check against the official migration guide. `Avalonia.AvaloniaEdit` has its own version sequence, and the current restore graph only declares a minimum Avalonia dependency of `11.0.0`; this does not prove support for 12. A compatible release should be checked: to check against the official migration guide.

## Transitive dependencies, renderer, native libraries and unreferenced packages

The following versions come from existing restore assets; both UI and Tests bring in these dependencies. They have no central PackageVersion or direct PackageReference, and pins should not be added solely for this inventory.

| Package | Resolved version | Current source／purpose |
| --- | --- | --- |
| `Avalonia.Skia` | `11.3.12` | Renderer brought in by Desktop and X11 |
| `Avalonia.Native` | `11.3.12` | Brought in by Desktop |
| `Avalonia.Win32` | `11.3.12` | Brought in by Desktop |
| `Avalonia.X11` | `11.3.12` | Brought in by Desktop |
| `Avalonia.FreeDesktop` | `11.3.12` | Brought in by X11 |
| `Avalonia.Remote.Protocol` | `11.3.12` | Brought in by the main Avalonia package |
| `Avalonia.BuildServices` | `11.3.2` | Build services brought in by the main Avalonia package |
| `Avalonia.Angle.Windows.Natives` | `2.1.25547.20250602` | Brought in by Win32 |
| `SkiaSharp` | `2.88.9` | Brought in by Avalonia.Skia |
| `SkiaSharp.NativeAssets.Linux` | `2.88.9` | Brought in by Avalonia.Skia |
| `SkiaSharp.NativeAssets.WebAssembly` | `2.88.9` | Brought in by Avalonia.Skia |
| `SkiaSharp.NativeAssets.Win32` | `2.88.9` | Brought in by SkiaSharp |
| `SkiaSharp.NativeAssets.macOS` | `2.88.9` | Brought in by SkiaSharp |
| `HarfBuzzSharp` | `8.3.1.1` | Text rendering dependency brought in by Avalonia.Skia |
| `HarfBuzzSharp.NativeAssets.Linux` | `8.3.1.1` | Brought in by Avalonia.Skia |
| `HarfBuzzSharp.NativeAssets.WebAssembly` | `8.3.1.1` | Brought in by Avalonia.Skia |
| `HarfBuzzSharp.NativeAssets.Win32` | `8.3.1.1` | Brought in by HarfBuzzSharp |
| `HarfBuzzSharp.NativeAssets.macOS` | `8.3.1.1` | Brought in by HarfBuzzSharp |
| `MicroCom.Runtime` | `0.11.0` | Brought in by the main Avalonia package |
| `Tmds.DBus.Protocol` | `0.21.2` | Brought in by Avalonia.FreeDesktop |
| `System.IO.Pipelines` | `8.0.0` | Brought in by Tmds.DBus.Protocol |

`Avalonia.BuildServices`, ANGLE, SkiaSharp, HarfBuzzSharp and other supporting packages have their own version sequences. The managed／native package pairings, platform support and dependency versions required by 12: to check against the official migration guide. Review the entire restore graph and native assets again during the upgrade; their target versions cannot be inferred from the current `11.3.12`.

`Avalonia.Diagnostics` has no central declaration, csproj reference or entry in the assets above, so there is no current version to list; whether diagnostics support for Debug builds needs to be added, replaced or adjusted: to check against the official migration guide. The current theme／font packages are only Fluent／Fonts.Inter; there is no `Avalonia.Themes.Simple` or other Avalonia font package.

## Lock files, restore and CI

There are currently **0 tracked `packages.lock.json` files**; none exists in the root directory or the directories containing the five csproj files either. `Directory.Build.props`, `Directory.Packages.props`, csproj files, workflows and build／test scripts do not enable `RestorePackagesWithLockFile`, `RestoreLockedMode` or `--locked-mode`. Central package management pins direct reference versions; it does not lock all transitive dependencies.

`build/obj/<專案名>/project.assets.json` and the generated `*.nuget.g.props`／`*.nuget.g.targets` are restore artifacts and cannot be treated as `packages.lock.json`. `scripts/perf/measure-code-size.ps1` collects tracked `*packages.lock.json` files as evidence; this collection does not create lock files or enable locked mode.

`.github/workflows` currently contains only `ci.yml`, with no step that explicitly names Avalonia, Headless, Fonts.Inter, Diagnostics or SkiaSharp, and no dedicated package cache／lock restore step. The actual dependencies are in the following entry points:

| CI job／step | Execution entry point | Current state relevant to the upgrade |
| --- | --- | --- |
| `policy / structure`／`Verify structure` | `verify.ps1 -StructureOnly -AllowMissingExampleData` | Checks SDK pin, action pins, shard consistency and structure; does not verify restore or package compatibility. |
| `dotnet / build`, each test shard／`Install pinned .NET SDK and runtime` | `scripts/ci/install-dotnet.ps1` | Gets the SDK from global.json and the runtime channel from the Tests csproj. |
| `dotnet / build`／`Format, analyzers and build` | `verify.ps1 -CiLane build -AllowMissingExampleData` | Delegates to `lint.ps1 -AllFiles -UseNoAppHost -WarningsAsErrors`; format／build does not prohibit restore and has no locked mode. |
| `dotnet / test (...)`／`Run test shard` | `verify.ps1 -CiLane test -Shard ...` | `run-tests.ps1` calls dotnet test without `--no-restore` or `--no-build`, so it goes through restore and build. |
| `dotnet / build-test`／`Require the build and every test shard to succeed` | Aggregates build／test results | Does not restore packages itself; the current viewmodel shard's `continue-on-error` is an existing temporary exception, so a passing aggregate cannot be described as all shards passing. |

The test matrix is `core, ui, viewmodel, snapshots`; `verify.ps1` maps snapshots to `ui-snapshots`, which includes `UiLayoutGuardTests`, `UiRenderedVisualSnapshotTests`, `UiSnapshotPersistenceContractTests` and `UiVisualSnapshotTests`. `HeadlessUiSmokeTests` is in `ui-stable` and runs in the ui shard.

If a future upgrade adopts lock files, first update／create each project's `packages.lock.json` with a non-locked restore, review version, dependency and content hash differences, then rerun in locked mode to confirm reproducibility. **There are currently no existing lock files to update and no existing locked CI to reuse directly**; whether to include lock files and CI locked restore should be decided by the actual upgrade work. This work does not add that mechanism. Whether 12 requires additional NuGet feeds or restore settings: to check against the official migration guide.

## Target framework, SDK and build conditions

| Item | Current state and affected locations | Upgrade items to check |
| --- | --- | --- |
| Target framework | All five csproj files for Domain, Application, Infrastructure, UI and Tests use `net8.0`, with no multi-targeting. | Whether the minimum TFM／runtime for 12 requires changes, and project reference compatibility: to check against the official migration guide. |
| SDK | `global.json`: `10.0.301`, `rollForward=latestPatch`, `allowPrerelease=false`. Local `dotnet --version` is `10.0.303`, an allowed patch roll-forward. SDK and `net8.0` are separate settings. | Whether the minimum SDK／MSBuild for 12 fits this range: to check against the official migration guide. |
| CI installation | `install-dotnet.ps1` installs the SDK version specified in global.json and installs the `8.0` runtime channel based on Tests' `net8.0`; SDK／TFM are not hardcoded in the workflow. | If the TFM needs adjustment, check all projects and this derivation logic; runtime requirements for 12: to check against the official migration guide. |
| C#／output | UI, Domain, Application and Infrastructure use `LangVersion=latest`; Tests does not specify it. UI is `WinExe`, with an application manifest／icon. | Effects of the new SDK on analyzers／generators and platform build conditions for 12: to check against the official migration guide. |
| Output locations | `BaseOutputPath=build/bin/<專案名>/`, `BaseIntermediateOutputPath=build/obj/<專案名>/`, conditional on `ArtifactsPath` being unset; `DefaultItemExcludes` excludes build. | MSBuild targets and artifact location assumptions for 12: to check against the official migration guide. |
| Publishing | `scripts/build/publish-exe.ps1` defaults to `win-x64`, Release, self-contained and `PublishTrimmed=false`; single-file also enables `IncludeNativeLibrariesForSelfExtract=true`. | Publishing and loading requirements for the new renderer／native assets: to check against the official migration guide; a passing regular build cannot replace publishing verification. |

`verify.ps1 -StructureOnly` only checks that the SDK version is a stable three-part number; it cannot prove that the SDK or TFM is suitable for Avalonia 12.

## Avalonia analyzers and MSBuild integration

The UI csproj explicitly specifies `<AvaloniaResource Include="Assets\**" />` and copies NLog.config via `None`. It does not explicitly specify Avalonia properties for compiled binding, XAML compilation or name generation, and has no custom Avalonia build task／analyzer package references.

The existing `Avalonia 11.3.12` package contains `Avalonia.Analyzers.dll`, `Avalonia.Generators.dll`, `Avalonia.Build.Tasks.dll`, and props／targets in `build/` and `buildTransitive/`; they come with the main package, not three independent central versions. Analyzer, generator, task and props／targets contracts for 12: to check against the official migration guide.

| Settings／processes provided by the current 11.3.12 package | Locally verifiable values | Upgrade items to check |
| --- | --- | --- |
| `EnableAvaloniaXamlCompilation` | `true` when not overridden | Support and defaults in 12: to check against the official migration guide. |
| `AvaloniaUseCompiledBindingsByDefault`, `AvaloniaXamlIlVerifyIl` | Both `false` when not overridden | Support and defaults in 12: to check against the official migration guide. This document does not inventory individual XAML／bindings. |
| `AvaloniaNameGeneratorIsEnabled`, `AvaloniaNameGeneratorBehavior`, `AvaloniaNameGeneratorDefaultFieldModifier` | `true`, `InitializeComponent`, `internal` | Generator settings contract for 12: to check against the official migration guide. |
| `AvaloniaNameGeneratorAttachDevTools` | Package default is `true`; this does not mean the repo references Diagnostics | Diagnostics／generator integration in 12: to check against the official migration guide. |
| resource／XAML targets | `GenerateAvaloniaResources`, `CompileAvaloniaXaml`; the package collects axaml／paml by default, participating in compilation and up-to-date inputs | Resource compilation and incremental build requirements for 12: to check against the official migration guide. |
| `Avalonia.BuildServices` | `11.3.2`, bringing in the `AvaloniaStats` target; commands in this work set `AVALONIA_TELEMETRY_OPTOUT=1` | Build services and opt-out support in 12: to check against the official migration guide. |

The shared .NET analyzer policy is `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true` and `GenerateDocumentationFile=true`; the existing `NoWarn` only appends `1572;1573;1591`. `.editorconfig` has no Avalonia／AVLN-specific severity settings. The upgrade must not add warning suppressions or lower lint standards.

The CI build lane's lint verifies with `dotnet format --severity warn`; the existing formatter excludes IDE0059／IDE0060, and the analyzer build uses `-warnaserror --no-incremental`. Diagnostics from new packages or SDKs must be handled under the existing gate; diagnostic changes in 12: to check against the official migration guide. Tests also resolves `xunit.analyzers 1.16.0`, which must be reverified together with runner／Headless compatibility.

## Required rerun sequence after the actual upgrade

The following is an execution checklist for a future authorized upgrade, **not commands executed in this work**. First complete the official guide checks above for all 12 target package versions, dependency pairings and toolchain requirements.

1. **Restore and lock differences.** Restore the solution again in an environment with network access and the required feeds, and check the complete UI／Tests dependency graphs, including SkiaSharp／HarfBuzzSharp native assets, AvaloniaEdit and the test runner. If the upgrade adopts lock files, create／update them with `dotnet restore FreeformHelper.sln --use-lock-file --force-evaluate`; after review, run `dotnet restore FreeformHelper.sln --locked-mode` to verify that the locks match the declarations. Restore is still required without lock files, but the lack of a locked-mode guarantee should be stated explicitly. These commands were not executed in this work.
2. **Prepare the workspace and build.** Run `scripts/dev/prepare-ui-workspace.ps1` according to AGENTS; rebuild UI and Tests to avoid using old Avalonia build artifacts. The command `dotnet build <專案> --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false` can be used; see above for UI and Tests paths. Also check native library loading for win-x64 folder／single-file publishing using the existing publishing script.
3. **Full repo gate.** Run `pwsh -NoProfile -File ./scripts/verify.ps1 -All` in an environment with authorized private data and the complete toolchain, covering structure, all-file lint／analyzer build and the four core／ui／viewmodel／snapshots shards. This entry point may implicitly restore, so it is unsuitable for this sandbox where restore is prohibited. Confirm the actual viewmodel results rather than relying only on the CI aggregator.
4. **UI guards and snapshots.** At minimum, confirm `HeadlessUiSmokeTests`, `UiLayoutGuardTests`, `UiSnapshotPersistenceContractTests`, `UiVisualSnapshotTests` and `UiRenderedVisualSnapshotTests`, and check the current appearance contract on the Dev page／real UI. Headless／renderer／font behavior in 12: to check against the official migration guide.
5. **Compare baselines before deciding.** Compare with the existing `scripts/tests/update-ui-baseline.ps1 -Mode DryRun`; this script's first test builds and may restore, so it should run in the external full environment. When `FH_UI_BASELINE_MODE` is unset, the mode is Check; `dry-run` does not write, and only `apply` writes. Possible differences from the upgrade must be retained and manually checked; expected values must not be overwritten automatically, and distance thresholds must not be relaxed to pass the gate. Only confirmed necessary visual differences may use `-Mode Apply` in a separate change, after which Check must be restored and the gate rerun.

The two baselines are `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json` (normalized source-file SHA-256) and `tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json` (headless-rendered average hash／Hamming distance). The former may remain unchanged when only packages change, but must still be rerun; the latter requires reverifying renderer／font results. The upgrade does not implicitly authorize baseline updates.

## Open items

- Official package list, alignment rules, compatible AvaloniaEdit version and Diagnostics requirements for 12: to check against the official migration guide.
- Target versions for SkiaSharp, HarfBuzzSharp, ANGLE, BuildServices and platform support packages: to check against the official migration guide.
- Minimum SDK／TFM／runtime, analyzer／generator, MSBuild, restore and publish requirements: to check against the official migration guide.
- Headless.XUnit／runner combinations and acceptable snapshot differences: to check against the official migration guide; appearance differences are also subject to the existing manual confirmation rules.
- Whether the actual upgrade includes `packages.lock.json` and CI locked restore: currently unimplemented and left for the upgrade work to decide. This document does not create new settings or verification mechanisms.
