# Coordinate Surface Transform Spec

This is the 1.1 data contract for future curved-screen coordinate output. It does not replace the flat Coordinate artifact workflow; it defines how a flat machine `(x, y)` coordinate can be projected into a surface `(x, y, z)` coordinate after `CoordinateArtifactSnapshot` is stable.

## Profile Parameters

| Field | Meaning |
| --- | --- |
| `curvatureDirection` | Axis that bends: `AlongX` or `AlongY`. |
| `mappingMode` | `ArcLength` treats flat distance as curved surface arc length. `Projection` treats flat distance as planar projected chord distance. |
| `zDirection` | Whether surface sag is positive or negative Z. |
| `radius` | Cylindrical radius in machine units. Must be positive at execution time. |
| `origin` | Flat machine-space origin for the curved axis. |
| `originZ` | Z offset at the origin. |

## Transform Semantics

- Input is a flat machine coordinate from a `CoordinateArtifactRow`.
- The non-curved axis stays unchanged.
- `ArcLength`:
  - `angle = offset / radius`
  - surface offset = `radius * sin(angle)`
  - `z = radius * (1 - cos(angle))`
- `Projection`:
  - offset is clamped to `[-radius, radius]`
  - surface offset = clamped offset
  - `z = radius - sqrt(radius^2 - offset^2)`
- `zDirection` applies the final sign before adding `originZ`.

## Release Scope

For 1.1, this remains an Application data model plus transform helper:

- `CoordinateSurfaceProfile`
- `CoordinateSurfacePoint`
- `CoordinateSurfaceTransformService.ProjectMachinePoint(...)`

UI recipe controls, artifact export columns for `(x, y, z)`, and multi-segment/non-cylindrical surfaces are future work.
