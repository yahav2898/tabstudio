# Audit of TabStudio 0.8.0

What the IDE looked like at 0.8.0 (main at `34ea202`), problem by problem. The screenshot names are those of the IDE
job's screenshots on CI (`screenshots/` dark, `screenshots/light/` light), plus, where marked, those of feature branches
that were not merged yet. Each problem names the stage 2 edit that fixes it ([stage-2.md](stage-2.md)), or the owner
when it belongs to a feature.

Severity: **High** hides or blocks something, **Medium** costs space or attention on every screen, **Low** is polish.

## The shell

| # | Problem | Screenshots | Severity | Fix |
|---|---|---|---|---|
| S1 | The side bar's tool window tabs wrap to two rows (Solution Explorer, Git Changes, Actions / Pull Requests, Test Explorer, Claude) and three once App, Preview or Swarm join. They take 70 to 100 px of the side bar and move when a tab appears. | 02-ide, avalonia-01-app-preview, blazor-01-web-app-beside-code, web-01-preview; swarm-00-add-a-device, winforms-01-running-app (branches) | Medium | Activity bar (2.3) |
| S2 | The floating touch bar covers code: lines 16 to 18 in 02-ide, the paused line's neighbors in debug-02-paused, the last lines in 06-portrait and 07-test-explorer. It collides with the light bulb (11-light-bulb), the coverage popup (live-02b-failing-popup) and the completion list (toolchains-lsp-01-python-pyright). It shows while typing on a hardware keyboard, when it is not needed. | 02-ide, debug-02-paused, 06-portrait, 07-test-explorer, 11-light-bulb, live-02b-failing-popup, toolchains-lsp-01-python-pyright | High | Touch bar (2.7) |
| S3 | In the light theme the touch bar's Run button is white text on light gray (`Brush.Pressed`), about 1.3:1. | light/02-ide, light/03-run | High (light only) | Touch bar (2.7) |
| S4 | Portrait clips the toolbar ("Run on In-pr…"), drops the solution chip off the menu bar, and gives no way to reach the side bar: Solution Explorer, Git Changes and Test Explorer are unreachable without a menu. | 06-portrait, 10-notebook-portrait, ext-09-portrait-details | High | Activity bar and drawer (2.3), adaptive toolbar (2.4) |
| S5 | The toolbar is full at 1280 while debugging (about 4 px to spare), and any new button (Tools in toolchains-lsp-02) pushes the error counter against the search box. It scrolls sideways when too narrow, hiding Run. | debug-02-paused, debug-04-call-stack, toolchains-lsp-02-python-breakpoint | Medium | Adaptive toolbar (2.4) |
| S6 | Properties takes 270 px under the Solution Explorer on every screen, mostly for a temp path wrapped over four lines, and for a folder shows rows with nothing in them ("Projects 0", "Startup Project None"). The WinForms designer shows its own Properties beside it: two at once. | 02-ide, lsp-10-python-squiggles, ext-01-browse; winforms-designer-01-selection (branch) | Medium | Collapsible Properties (2.6) |
| S7 | The status bar shows jargon and noise: "Roslyn warm", "Thermal OK" all the time, "Pyright ready", and grows with every language server and long message until items are pushed off ("Pulled main from GitHub. 1 file has conflicting changes: resolve it under Merge…"). | 02-ide, 07-git-changes, lsp-01-squiggles-error-list, lsp-11-python-completion, 31-toolchain-installing | Medium | Adaptive status bar (2.5) |
| S8 | Search is for files only (Ctrl+Q says "Search files"): commands, settings and symbols cannot be found, and the 240 px box is always on the menu bar. | 02-ide | Medium | Unified search (2.2) |
| S9 | The Run split button can be half disabled: a gray main part with a teal chevron. | ext-01-browse, avalonia-01-app-preview, web-01-preview, blazor-01-web-app-beside-code; winforms-01-running-app (branch) | Low | Adaptive toolbar (2.4): one state for both parts |
| S10 | The IDE was dark only: an extension's light color theme gave a light editor in dark chrome, and a dark one a navy editor in gray chrome. | ext-10-theme-picker, ext-14-csharp-lumen-night (0.8.0) | Medium | Done in stage 1: the light theme, and color themes set the IDE's kind (ext-15-lumen-day-light-ide) |
| S11 | On a real tablet the Start screen's template tags have their text at the top of the pill: pills and chips have a fixed height and their text stretched to it, which Android's font metrics make visible. Headless screenshots hide it. A sweep of every view (a script over the XAML, then `CenteredTextTests` over the live IDE) found the rest of the pills (Toolbox and Properties counts, Link states, References counts, the notebook's HTML tag, the Extensions tab counts) and nothing else: badges, tags, avatars, key caps and the status bar already center their text. | owner's photo of 0.8; 01-start | Medium | Pills and chips center their content (b1c63ce on agent/integrate-winforms, 0.9); the rule is in the spec and the tests |

## Panels

| # | Problem | Screenshots | Severity | Fix |
|---|---|---|---|---|
| P1 | Empty states are a lone muted line with no way forward: "Solutions and folders you open appear here." (Start), "Open a file from the Solution Explorer" with no button, the Test Explorer before a build. | 01-start, 08-test-explorer-outcome | Medium | `EmptyState` with an action, panel by panel |
| P2 | Section headers disagree: ALL CAPS in GitHub Actions ("THIS COMMIT", "RUNS", "WORKFLOWS", "WATCHED REPOSITORIES"), sentence case in Git Changes ("Changes (2)", "Merge Conflicts (1)"). | actions-01-status-running, actions-08-dispatch-form, 07-git-changes | Low | Sentence case everywhere (owner: Actions) |
| P3 | "Signed in to GitHub as tester · Sign out" is repeated at the bottom of Git Changes, Actions and Pull Requests. | 07-git-changes, actions-01-status-running, pr-01-list | Low | One account entry (stage 2 status bar or activity bar bottom) |
| P4 | Panel headers are each built by hand: Git Changes has title, repository and an icon row; Actions and Pull Requests differ in spacing and filters (dropdowns in Actions, pills in Pull Requests). | 07-git-changes, actions-04-runs, pr-01-list | Low | `PanelHeader` |
| P5 | Long names are cut in the 330 px side bar without a tooltip: test names ("Total_multiplies_price_and_q…"), extension names ("Pyrefly - Python Language T…"), the Error List's Code column ("reportAttributeAccess…"). | live-01-markers, ext-01-browse, lsp-11-python-completion | Low | Tooltips on trimmed text; a wider Code column or a tooltip |
| P6 | Every result in the extension browser has a solid teal Install button: five primary buttons at once. | ext-01-browse, ext-02-details | Low | Secondary Install in rows, primary only on the details page (owner: Extensions) |
| P7 | The agent's header shows token counts ("claude-opus-5-5 · 960 in · 168 out"): jargon for most users. | agent-01-proposed-change | Low | Move to a tooltip (owner: agent) |
| P8 | The Claude panel of a swarm device is a wall of text in a narrow column. | swarm-04-fix-with-agent (branch) | Low | Owner: swarm |

## Dialogs and overlays

| # | Problem | Screenshots | Severity | Fix |
|---|---|---|---|---|
| D1 | Toasts cover content: the CI toast covers code at the editor's top right; the HDMI offer covers the Error List's rows. | actions-02-status-failed, desktop-01-offer | Medium | One toast position (spec: bottom right of the document area, never over code being edited) |
| D2 | The workflow dispatch form uses the YAML ids as labels ("version*", "dry-run", lowercase). | actions-08-dispatch-form | Low | Title case from the id, the description below (owner: Actions) |
| D3 | The XAML designer's error card overlaps the preview it explains. | avalonia-03-xaml-designer-error | Low | `ErrorState` in a strip above the preview (owner: XAML designer) |
| D4 | The web preview's address field is about 80 px wide and shows "http://127.0.0." | web-01-preview | Low | Owner: web preview |

## Start screen

| # | Problem | Screenshots | Severity | Fix |
|---|---|---|---|---|
| T1 | "Open the sample" appears twice (a tile and the card's button); the right two thirds below the card are empty. | 01-start | Low | Keep the tile; the card explains |
| T2 | The recent list's empty state has no action; the Link chip's "Local" badge is about 9 px. | 01-start, link-03-start-linked | Low | `EmptyState`; caption size |

## Consistency

| # | Problem | Where | Fix |
|---|---|---|---|
| C1 | Six icons are defined twice, in two theme files: `Icon.Trash` (Notebooks, Extensions), `Icon.Plus` and `Icon.RunAll` (Notebooks, Icons), `Icon.ArrowUp` and `Icon.ArrowDown` (Notebooks, DesktopMode), `Icon.Tablet` (DesktopMode, Icons). | `src/TabStudio.App/Theme/` | Keep one each in Icons.axaml |
| C2 | Two spinner styles: `StrokeIcon.spinning` (ActionsStyles.axaml) and `.spinner` (NotebookView.axaml). | | `LoadingState` and `StrokeIcon.spinning` only |
| C3 | Literal colors in views (scrims, shadows, `White`, hex backgrounds) made a light theme impossible. | 40 files | Done in stage 1, except the shell files listed in [stage-2.md](stage-2.md#21-colors-left-in-the-shell) |

## What works well

Kept as models for the rest:

- The Toolchain Store dialog (30-toolchain-store, 31-toolchain-installing): a clear title, rows with an icon tile,
  state pills and one action each, a footer with the totals.
- The desktop mode companion (desktop-03-tablet-companion): big touch targets, and an empty state with an icon, a
  title and a sentence. `EmptyState` is built on it.
- The terminal's key row (33-terminal): the keys a soft keyboard lacks, 44 px.
- The exception helper (debug-05-exception-helper): a title, the message, details, and the two actions.
- The Swarm device matrix (swarm-01-device-matrix, branch): a dense grid that stays readable.

## Light theme

Every merged screen was rendered in the light theme (`screenshots/light/`, 129 screenshots on this branch's CI) and
looked at. All text passes AA (see [tokens.md](tokens.md#contrast)); what is left:

- The touch bar's Run button (S3): fixed in the shell, stage 2.
- The editor's current line, caret and fold markers are literals in `CodeEditorView`, used only with the built-in dark
  editor theme: in the light theme they come from TabStudio Light, an editor color theme built from the tokens
  (light/02-ide), so they look right. Stage 2 still makes them tokens, so the palette is the one source.
- The prompt's scrim and the touch bar's shadow in `WorkspaceView.axaml` are the dark ones.
