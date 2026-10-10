# Temporary Core source follow-up

- [ ] After the Core release that ships `UiEventRunner` (planned 0.9.0), delete the CoreSource project (or `Vendor/Core`), drop `NVT_CORE_SOURCE_CONSUMPTION`, and bump the Core package pin. Remove copy-specific integrity guards and exclusions. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Legacy console follow-up

- [ ] When the Core console list replaces the legacy console panel, delete the text buffer of `ShellViewModel.Console.cs` with its gate (`_consoleTextGate`) and the `OnLogEntriesChanged` hook. Until then the gate keeps log notifications from other threads away from the buffer.
