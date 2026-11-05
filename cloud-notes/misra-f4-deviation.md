# MISRA C:2012 deviation record draft: NHC-DEV-01 (Rule 10.1, right shift of a signed operand)

Date: 2026-11-05. Status: **DRAFT, not approved.** Base: `main` at `5e48bec`. Source finding: F4 in `cloud-notes/misra-generated-c-audit.md` (branch `origin/cloud/misra-generated-c-audit`).

This file is the fallback for the owner's decision on F4. The recommended path is the generator fix on this branch (see "Recommendation"). Use this record only if the owner chooses to keep the shift.

## Recommendation

Fix the generator (option a), and do not file this deviation.

1. The fix is bit-identical to the shift for the whole input domain. `FUNC_NHC_ScaleQ7Floor(p)` equals `p >> 7` for every product `p` in `[-8400000, 8400000]`, which contains the whole range of `INT16 * UINT8` (`[-8355840, 8355585]`). The test `ExportAsCInitializer_V21Q7Scale_EqualsArithmeticShiftForEveryInt16TimesUint8Product` checks this with gcc. Simulation, golden values and the "no decode to percent/double" contract stay unchanged.
2. A deviation is only as good as the FW-side record. This repo contains neither the FW MISRA compliance file nor the FW compiler manual, so it cannot show that `NHC-DEV-01` exists or that the toolchain documents arithmetic right shift (see "Open items"). The generator fix removes that dependency.
3. The generated file stops carrying four deviation markers and the C text no longer relies on implementation-defined behaviour (MISRA C:2012 Dir 1.1).
4. The cost is small and measurable: one extra `static` function and one compare-and-branch per leg. If the cycle or code-size budget on the target is tight, measure it first. This is the only argument for the deviation.

Choose the deviation instead if the FW team already has `NHC-DEV-01` approved, the toolchain manual documents the shift, and the per-leg branch is not acceptable on the target.

## Record

| Field | Value |
| --- | --- |
| ID | `NHC-DEV-01` (the ID the generator already writes in the C comments) |
| Rule | MISRA C:2012 Rule 10.1: operands shall not be of an inappropriate essential type. The `>>` operator shall not have an essentially signed left operand. |
| Category | Required |
| Project | FreeformHelper generated Notch table C file, layout v2.1 (`ST_PRI_NHC_TABLE_NODE_INFO` with `NHC_1ST_*` and `NHC_2ND_*`). v2.2 has no shift and is not affected. |
| Tool that reports it | cppcheck 2.13.0 `--addon=misra`: 4 reports per generated v2.1 file. |

### Scope

- Where: function `FUNC_NHC_DiffCompensationOneTable` in the generated `.c` file, four statements, emitted by `src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs` (`AppendV21FunctionBlock`):

  ```c
  s16Sum += (INT16)((((INT32)S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_1ST_IDX]) * (INT32)pstTable[u16j].NHC_1ST_RATIO) >> 7);
  ```

  The same form appears with `-=`, and with `NHC_2ND_IDX` and `NHC_2ND_RATIO`.
- Left operand: `INT32`, the product of an `INT16` diff value and a `UINT8` Q7 ratio. Range `[-8355840, 8355585]`. Shift count: constant `7`, valid for a 32-bit operand.
- Not covered: any other violation. Other findings (F1–F3, F5–F10) need their own decisions.

### Rationale

- The v2.1 firmware apply is defined as `(INT16 source * magnitudeQ7) >> 7` (`docs/reference/notch-system-reference.md`, `docs/core/notch-v21-algorithm.md`). The contract requires bit-exact agreement of the simulation, the golden values and the gcc runtime, and says it must not be rewritten through percent or `double`.
- For a negative product the shift rounds toward negative infinity (floor). Division by 128 rounds toward zero. The two differ for every negative product that is not a multiple of 128, for example `-3 * 38 = -114`: shift gives `-1`, division gives `0`. Replacing the shift by `/ 128` therefore changes values and breaks the contract.
- The shift is the one primitive that gives floor semantics without extra code, on a target with a cycle budget.

### Risk

- **Implementation-defined result.** C99 6.5.7 makes the result of `>>` on a negative signed value implementation-defined. If the toolchain does a logical shift or a different rounding, the result is wrong for every negative diff value, silently. No compiler warning is issued.
- **Dir 1.1.** MISRA C:2012 Dir 1.1 requires every implementation-defined behaviour that the code relies on to be documented and understood. This record is only valid together with a reference to the FW compiler manual for that behaviour.
- **Portability.** A change of compiler, of target or of optimization level can change the behaviour. The gcc runtime tests do not prove the FW toolchain.
- **Scope drift.** The generator writes the marker comment itself. A later change of the shift count, the operand type or the ratio type (for example an `INT8` ratio, as in v2.2) would still carry the marker and would not be re-reviewed.

### Mitigation

1. Cite the FW toolchain manual section that documents arithmetic right shift of negative `INT32`. Attach the reference to the approved record. (Open item 2.)
2. Keep the gcc runtime parity tests (`NotchTableExporterTests`, Q7 boundary test and the negative-source theory) as the regression guard. Add the same case to the FW unit test or HIL test on the real compiler: `-3 * 38 >> 7 == -1`.
3. Add a compile-time check in the FW build (for example a static assertion on `(-1) >> 1 == -1` in a FW source file, not in the generated file) so that a toolchain change cannot silently break the assumption.
4. Keep the shift count `7`, the `INT32` operand and the four statements as the only scope of the deviation. A change in the generator that touches these lines needs a new review of this record.
5. Review the record at each toolchain upgrade.

### Approval

| Field | Value |
| --- | --- |
| Raised by | (name, date) |
| Reviewed by | (FW MISRA owner, date) |
| Approved by | (project owner, date) |
| Toolchain and version | (to fill in) |
| Review due | (next toolchain upgrade) |
| Stored in | FW MISRA compliance file (to fill in; this repo does not hold it) |

## Open items

1. The FW MISRA compliance file is not in this repo. It is not known whether `NHC-DEV-01` already exists there. If it does, check that its scope matches the four statements above.
2. The FW compiler and the section of its manual on right shift of negative signed values are not known.
3. If the owner chooses this deviation, this branch's generator change must not be adopted. The generator change and this record are alternatives, not additions.
