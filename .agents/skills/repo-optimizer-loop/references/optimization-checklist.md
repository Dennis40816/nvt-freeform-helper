# Optimization Checklist

## Scan dimensions
1. Efficiency
- selection/inspector delay
- avoid synchronous waits on UI/IPC lifecycle
- repeated heavy calculations without cache/defer policy

2. Workflow
- multi-entry behavior that should be single-entry
- unique-path audit must separate acceptable multi-entry/single-path designs from unacceptable multi-path re-derivations
- shared result models surfaced in overlay/query/inspector/export must have a regression guard that compares each reader against the same resolved model
- direct state mutation hotspots (public mutable collection properties, external side-effect assembly) with convergence route
- missing side-effect centralization
- roadmap/doc drift versus implemented behavior

3. UI clarity
- overflow/clipping issues
- hierarchy/readability issues in high-frequency panels
- token/style consistency (no hardcoded values)

4. Redundancy / maintainability
- giant files and mixed responsibilities
- duplicated code paths and duplicate formatting logic
- stale features or dead references

## Evidence requirements
- Provide file paths and why they are hotspots.
- Provide measured or count-based evidence when possible:
  - line count hotspots,
  - lint/analyzer warning summary,
  - log timings for latency issues.

## Mandatory outputs per scan
- one dated report in `docs/guides/`
- report must include a dedicated `Direct state mutation hotspots` section
- refreshed ROADMAP.md with executable unchecked tasks
- explicit "done definition" for each new roadmap item
- PR must reference `docs/guides/refactor-pr-checklist.md` and mark S11.52 direct mutation gate status
