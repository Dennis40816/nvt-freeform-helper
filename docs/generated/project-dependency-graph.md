# Project Dependency Graph

- Generated: 2026-10-09 19:49:15 UTC
- Source solution: `FreeformHelper.sln`

```mermaid
graph LR
  P1["FreeformHelper.Application"]
  P2["FreeformHelper.CoreSource"]
  P3["FreeformHelper.Domain"]
  P4["FreeformHelper.Infrastructure"]
  P6["FreeformHelper.Tests"]
  P5["FreeformHelper.UI"]
  P1 --> P3
  P4 --> P1
  P4 --> P3
  P6 --> P1
  P6 --> P3
  P6 --> P4
  P6 --> P5
  P5 --> P1
  P5 --> P2
  P5 --> P3
  P5 --> P4
```

## Project Paths

- `FreeformHelper.Application`: `src\FreeformHelper.Application\FreeformHelper.Application.csproj`
- `FreeformHelper.CoreSource`: `src\FreeformHelper.CoreSource\FreeformHelper.CoreSource.csproj`
- `FreeformHelper.Domain`: `src\FreeformHelper.Domain\FreeformHelper.Domain.csproj`
- `FreeformHelper.Infrastructure`: `src\FreeformHelper.Infrastructure\FreeformHelper.Infrastructure.csproj`
- `FreeformHelper.Tests`: `tests\FreeformHelper.Tests\FreeformHelper.Tests.csproj`
- `FreeformHelper.UI`: `src\FreeformHelper.UI\FreeformHelper.UI.csproj`

## Usage

- Run `./scripts/build/generate-dependency-graph.ps1` to refresh this graph.
- Task kickoff should consult this graph first, then do targeted file search.
