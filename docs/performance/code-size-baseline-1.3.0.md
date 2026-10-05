# FreeformHelper 1.3.0 Code-size Baseline

- Approval date: 2026-08-08
- Measurement entry point: `scripts/perf/measure-code-size.ps1`
- Contract check: `scripts/tests/check-code-size-baseline.ps1`
- 1.3.x signed-start commit: `3032121156dec2329e38e971327d55c7887db99f`
- First independent cutdown before commit: `207e29d5115c3d3318ee3249ed3af197f2d18180`

## 1. Measurement Contract

Production source is the primary metric, counting only git tracked files that match the following mutually exclusive groups:

- `src/FreeformHelper.Domain/**/*.cs`
- `src/FreeformHelper.Application/**/*.cs`
- `src/FreeformHelper.Infrastructure/**/*.cs`
- `src/FreeformHelper.UI/**/*.cs`
- `src/FreeformHelper.UI/**/*.axaml`

`UI ViewModels` and `UI Services` are subsets of UI C#; they are included when calculating logic-first but are not added to the production total a second time. tests, docs, scripts, Assets, Goldens, Generated, bin, obj, build, and generated suffixes (`*.g.cs`, `*.g.i.cs`, `*.generated.cs`, etc.) are all excluded. The tool runs the line/path classifier self-test before measuring the actual tracked tree.

Line count definitions:

- physical: CRLF, LF, and lone CR are all delimiters; an EOF newline does not produce an extra sentinel line.
- empty eligible file: the file count is still 1, and physical/nonblank are both 0.
- nonblank: apply `String.IsNullOrWhiteSpace` to each physical line and count it only if the result is false.

The source metric can read the tracked working tree while recording the base commit/source-tree OID and `productionSourceDirty`; approved numbers always come from an immutable commit archive. The Release metric accepts only clean tracked build inputs and is only a secondary metric.

## 2. Three Anchors That Must Not Be Mixed

| Anchor | Total files | Physical | Nonblank | Logic files | Logic physical | Logic nonblank |
|---|---:|---:|---:|---:|---:|---:|
| signed-worktree observation before GitHub #4 was created | 586 | 98,946 | 88,311 | 381 | 64,705 | 57,366 |
| 1.3.x signed-start `3032121` | 586 | 98,950 | 88,315 | 381 | 64,709 | 57,370 |
| R13.006b independent before `207e29d` | 589 | 98,617 | 88,021 | 384 | 64,377 | 57,077 |

The original observation has no immutable tree OID and therefore cannot serve as a future automated gate. `3032121` is the first immutable anchor after completion of the R13.002 production 3635 gate; relative to the observation, both total and logic-first are `+0 files / +4 physical / +4 nonblank`. The roadmap's `165f076` remains the planning document baseline, not the code-size signed start.

The `src` tree of `207e29d` is `c5048b4ecfd0c8a89a8a4719a48afbf11b980f68`. It follows completion of R13.004, so R13.006b must use it as the independent before; using `3032121` instead would count the reductions already completed in #6 again in #7.

## 3. Source breakdown

| Group | `3032121` files / physical / nonblank | `207e29d` files / physical / nonblank | Delta |
|---|---:|---:|---:|
| Domain | 18 / 1,087 / 994 | 18 / 1,087 / 994 | 0 / 0 / 0 |
| Application | 92 / 17,606 / 15,632 | 95 / 17,475 / 15,517 | +3 / -131 / -115 |
| Infrastructure | 14 / 2,130 / 1,873 | 14 / 2,130 / 1,873 | 0 / 0 / 0 |
| UI C# | 393 / 61,930 / 54,514 | 393 / 61,730 / 54,337 | 0 / -200 / -177 |
| UI AXAML | 69 / 16,197 / 15,302 | 69 / 16,195 / 15,300 | 0 / -2 / -2 |
| **Total** | **586 / 98,950 / 88,315** | **589 / 98,617 / 88,021** | **+3 / -333 / -294** |
| UI ViewModels subset | 163 / 31,076 / 27,493 | 163 / 30,889 / 27,324 | 0 / -187 / -169 |
| UI Services subset | 94 / 12,810 / 11,378 | 94 / 12,796 / 11,369 | 0 / -14 / -9 |
| **Logic-first** | **381 / 64,709 / 57,370** | **384 / 64,377 / 57,077** | **+3 / -332 / -293** |

The total `3032121 -> 207e29d` reconciles fully: R13.003 is `+320 physical / +288 nonblank`, R13.004 is `-653 / -582`, and the sum is `-333 / -294`. R13.006a itself must not modify `src`; the source-tree OID of its completion commit must still equal that of `207e29d`.

### R13.006b First Independent cutdown

R13.006b only removes the second identical sort by `BuildHeatmap` on the `cells` list that has already been materialized and sorted by row/col, without counting R13.004 again. Public characterization uses pad input in shuffled order to lock down the row/col order of `Result.Cells` and `Heatmap.Cells`; the completed source-tree OID is `b35b9a50cb646be14db5c15cdd5533ab64d73867`.

| Scope | Before `207e29d` files / physical / nonblank | R13.006b files / physical / nonblank | Delta |
|---|---:|---:|---:|
| Application | 95 / 17,475 / 15,517 | 95 / 17,473 / 15,515 | 0 / -2 / -2 |
| **Total** | **589 / 98,617 / 88,021** | **589 / 98,615 / 88,019** | **0 / -2 / -2** |
| **Logic-first** | **384 / 64,377 / 57,077** | **384 / 64,375 / 57,075** | **0 / -2 / -2** |

## 4. Secondary Release artifacts

Measurement profile: the same clean checkout and the same SDK/OS/RID/TFM, with Release builds in two new, isolated `ArtifactsPath` locations. Each root independently holds bin/obj; an immutable global NuGet cache may be shared. The bytes and SHA-256 of the four primary DLLs must be exactly identical across both builds.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `FreeformHelper.Domain.dll` | 39,936 | `0EAD9AC98CB828F244555F7FB910C5F9EC6E469FC6F246C88B31CF4B564766D5` |
| `FreeformHelper.Application.dll` | 691,200 | `49887D9A72BF581283F35B681CE0D8F344EDCFFF62FDD2C53812CECD8731A4A4` |
| `FreeformHelper.Infrastructure.dll` | 60,928 | `CAFA18138E4847422C698657F3EED87565D3CFAB5633260D0537F8BCA3A86F29` |
| `FreeformHelper.UI.dll` | 14,523,392 | `3A0CBCAC6E054A9AB762A8EC9C7C3EBF5872C2B459DC470E1C9D60838FC072B6` |
| **Total** | **15,315,456** | — |

The two isolated builds of the R13.006b clean candidate are also exactly repeatable. The bytes of all four artifacts are unchanged, with a total delta of 0; this is an honest result under PE alignment and does not replace the `-2 / -2` primary evidence for production source in the preceding section. Only the hash of the Application DLL containing the removed implementation changes:

| Artifact | R13.006a bytes / SHA-256 | R13.006b bytes / SHA-256 | Byte delta |
|---|---|---|---:|
| `FreeformHelper.Domain.dll` | 39,936 / `0EAD9AC9…6766D5` | 39,936 / `0EAD9AC9…6766D5` | 0 |
| `FreeformHelper.Application.dll` | 691,200 / `49887D9A…31A4A4` | 691,200 / `B7B07B17…62A9B` | 0 |
| `FreeformHelper.Infrastructure.dll` | 60,928 / `CAFA1813…A86F29` | 60,928 / `CAFA1813…A86F29` | 0 |
| `FreeformHelper.UI.dll` | 14,523,392 / `3A0CBCAC…FC072B6` | 14,523,392 / `3A0CBCAC…FC072B6` | 0 |
| **Total** | **15,315,456** | **15,315,456** | **0** |

This is an explicit no-PDB measurement profile: `Release`, `net8.0`, `UseAppHost=false`, `ContinuousIntegrationBuild=true`, `Deterministic=true`, `IncludeSourceRevisionInInformationalVersion=false`, `DebugType=None`, `DebugSymbols=false`, with each artifact root and the repo root mapped through PathMap to fixed virtual paths. Source revision metadata is excluded so that changing only docs/commit SHA does not change the structural size hash; this total must not be compared with the default portable-PDB Release size.

Approval environment: .NET SDK `10.0.302`, MSBuild `18.6.11.33009`, Microsoft Windows `10.0.26200`, OS/process `X64`, RID `win-x64`. The repo has no `global.json` or `packages.lock.json`, so this hash claims reproducibility only with the recorded checkout and the same toolchain/environment, not a guarantee of binary reproducibility across SDKs/OSes.

## 5. How to Rerun

Check only the source contract (usable for an uncommitted slice):

```powershell
./scripts/tests/check-code-size-baseline.ps1
./scripts/perf/measure-code-size.ps1 -SkipReleaseBuild
```

Generate the authoritative source + Release manifest (tracked build inputs must be clean; the entry point first runs workspace preparation):

```powershell
./scripts/perf/measure-code-size.ps1 `
  -OutJsonPath build/code-size/code-size-baseline.json
```

JSON and assembly outputs are in the ignored `build/code-size/`; do not commit dynamic machine output. Review evidence should cite the command, commit/source-tree OID, summary values, and manifest path.
