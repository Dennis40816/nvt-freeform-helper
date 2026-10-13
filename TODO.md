# Temporary Core source follow-up

- [x] After the Core release that ships `UiEventRunner` (0.9.1), delete the CoreSource project, drop `NVT_CORE_SOURCE_CONSUMPTION`, and bump the Core package pin. Remove copy-specific integrity guards and exclusions. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Legacy console follow-up

- [x] The Core console list replaced the legacy console panel. The text buffer of `ShellViewModel.Console.cs`, its gate and the `OnLogEntriesChanged` hook are deleted. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [ ] After Core 0.9.2 ships the console zoom, pin 0.9.2 and connect it. `ConsoleSession` keeps the zoom value (Core does not store it), the view assigns it when it is created, and the `ZoomChanged` event saves it (range 80% to 300%). Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
