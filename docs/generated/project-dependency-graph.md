# Project Dependency Graph

- Generated: 2026-06-05 10:14:32 UTC
- Source solution: `FreeformHelper.sln`

```mermaid
graph LR
  P1["FreeformHelper.Application"]
  P2["FreeformHelper.Domain"]
  P3["FreeformHelper.Infrastructure"]
  P5["FreeformHelper.Tests"]
  P4["FreeformHelper.UI"]
  P1 --> P2
  P3 --> P1
  P3 --> P2
  P5 --> P1
  P5 --> P2
  P5 --> P3
  P5 --> P4
  P4 --> P1
  P4 --> P2
  P4 --> P3
```

## Project Paths

- `FreeformHelper.Application`: `src\FreeformHelper.Application\FreeformHelper.Application.csproj`
- `FreeformHelper.Domain`: `src\FreeformHelper.Domain\FreeformHelper.Domain.csproj`
- `FreeformHelper.Infrastructure`: `src\FreeformHelper.Infrastructure\FreeformHelper.Infrastructure.csproj`
- `FreeformHelper.Tests`: `tests\FreeformHelper.Tests\FreeformHelper.Tests.csproj`
- `FreeformHelper.UI`: `src\FreeformHelper.UI\FreeformHelper.UI.csproj`

## Usage

- Run `./scripts/build/generate-dependency-graph.ps1` to refresh this graph.
- Task kickoff should consult this graph first, then do targeted file search.
