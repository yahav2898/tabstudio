# Audit of the panels added in 0.15 and 0.16

0.15 and 0.16 added a dozen panels, each written by a different agent, and none was checked against this folder's
spec. This audit looks at each one the same way, lists what was wrong, and shows it before and after. Every problem in
the tables is fixed on `agent/design-polish`.

## How the panels were checked

Each panel is shown in the real IDE by `tests/TabStudio.App.Tests/Design/PanelAuditTests.cs`, with the state that
shows it best (a SQLite database with tables, a Compose file with a service the tablet cannot run, a recorded debugging
session, a port with notes…), on three screens: a landscape tablet (1280 × 800), a portrait tablet (800 × 1280) and a
phone (412 × 915), in the dark and the light theme. That is six screenshots a panel, saved in `screenshots/audit/` of
the IDE job. `PanelAudit` then checks the panel in each:

- the layout checks of [docs/layout-checks.md](../layout-checks.md): no clipped text (SWRM001), nothing off screen
  (SWRM002), no overlapping controls (SWRM003) and no text below WCAG AA (SWRM005), as the layout checks report them
  for an app;
- touch targets of at least 34 × 34 (list rows 32), the smallest the IDE's own controls are (SWRM004);
- text trimmed with an ellipsis has its whole text in a tooltip ([Type](README.md#type));
- a line of text in a fixed height is centered in it, not stretched ([Type](README.md#type)).

`DesignRuleTests` reads the same panels' XAML for what a screenshot cannot show: colors are tokens, font sizes are on
the type scale, spacing is on the 4-pixel grid, radii are the radius tokens, every button has one of the IDE's button
themes, icons come from the icon set, and headers are in sentence case.

Both tests fail on any problem, so the panels stay as they are now. The audit's first run, on main before any fix,
found 350 problems in the screenshots and 57 in the XAML; the tables below group them.

## What every panel shared

Most problems were the same in every panel, because each was written without the shared components:

- **No panel header.** Each made its own: "REMOTE EXPLORER" in capitals over a 70%-opacity subtitle, a 16-point
  "Services", a bare "Database Explorer" line. They now use `PanelHeader`, with up to three icon actions and a More menu.
- **Fluent's default buttons.** Gray filled buttons, 30 pixels high, in rows that wrap: "Start all", "Connect /
  Refresh", "Send notes to Agent". They are now `SecondaryButton`, `ToolButton` or the surface's one `AccentButton`,
  36 high, with Visual Studio's title case ("Start All", "Open Folder", "Send Notes to Agent").
- **Small fields.** Text boxes 30 high, combo boxes and check boxes 32: under the 34 the IDE's touch targets need. The
  forms are 36.
- **No empty states.** A lone gray line, or an empty list, where `EmptyState` says what would be there and how it gets
  there.
- **Literal styling.** Opacity for muted text, `FontFamily="monospace"`, a 6-pixel radius, 5- and 7-pixel spacing, 16,
  17 and 23-point type.
- **A text prompt for a choice.** Choosing an APK, a PIC USB device or an MPLAB configuration meant typing a number or
  a name into a text box. The in-app prompt now has a list to choose from (`PromptViewModel.ChooseAsync`), with Enter or
  a double tap to choose.
- **Three tool windows with one icon.** Services, Windows Bridge and Remote Explorer all used `Icon.Computer` in the
  activity bar. Services has `Icon.Server` now and Remote Explorer `Icon.Remote`, and Services moved from between the
  Solution Explorer and Git Changes to the other 0.15 and 0.16 tool windows.

<!-- SECTIONS -->
