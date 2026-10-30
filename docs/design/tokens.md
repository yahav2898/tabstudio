# Color tokens

Every color of the IDE is a token in `src/TabStudio.App/Theme/Colors.axaml`, defined once for each theme (the dark
dictionary is `Default`, so a lookup without a theme finds the dark value). Views use `{DynamicResource Token}`; code
uses `ThemeBrushes.Get("Token")` (a live brush that follows the theme), `ThemeBrushes.Editor("Editor.…")` for the
editor's decorations (which follow the editor's color theme, not the IDE's) or `ThemedPen`. This file is generated
from the palette and lists them all; [README.md](README.md#color) says which to use when.

Hex values with eight digits are `#AARRGGBB`.

## Contrast

222 foreground and background pairs are checked against WCAG 2.1 in both themes (4.5:1 for text, 3:1 for icons,
glyphs and squiggles). None is below its threshold. `ThemePaletteTests` checks the main ones on every CI run. The main ones:

| Foreground | Background | Needs | Dark | Light |
|---|---|---|---|---|
| `Brush.TextBright` | `Brush.Window` | 4.5:1 | 14.2 | 18.6 |
| `Brush.Text` | `Brush.Panel` | 4.5:1 | 11.0 | 15.2 |
| `Brush.TextSecondary` | `Brush.Toolbar` | 4.5:1 | 9.8 | 10.5 |
| `Brush.TextMuted` | `Brush.Panel` | 4.5:1 | 6.3 | 5.8 |
| `Brush.TextMuted` | `Brush.Hover` | 4.5:1 | 5.6 | 5.1 |
| `Brush.TextMuted` | `Brush.ActivityBar` | 4.5:1 | 7.1 | 5.4 |
| `Brush.LineNumber` | `Brush.Window` | 4.5:1 | 5.0 | 4.9 |
| `Brush.Link` | `Brush.Popup` | 4.5:1 | 7.2 | 6.6 |
| `Brush.AccentText` | `Brush.AccentSoft` | 4.5:1 | 7.1 | 5.3 |
| `Brush.OnAccent` | `Brush.Accent` | 4.5:1 | 5.2 | 5.2 |
| `Brush.OnAccent` | `Brush.Debugging` | 4.5:1 | 4.9 | 5.5 |
| `Brush.OnAccent` | `Brush.Badge.Count` | 4.5:1 | 4.9 | 5.6 |
| `Brush.SelectionText` | `Brush.Selection` | 4.5:1 | 11.3 | 14.0 |
| `Brush.Error` | `Brush.Panel` | 4.5:1 | 5.4 | 5.3 |
| `Brush.Warning` | `Brush.Panel` | 4.5:1 | 7.9 | 5.4 |
| `Brush.Success` | `Brush.Panel` | 4.5:1 | 8.6 | 5.4 |
| `Brush.Info` | `Brush.Panel` | 4.5:1 | 7.1 | 5.6 |
| `Syntax.Comment` | `Brush.Window` | 4.5:1 | 6.4 | 5.1 |
| `Syntax.Keyword` | `Brush.CurrentLine` | 4.5:1 | 7.0 | 6.1 |
| `Syntax.String` | `Brush.Window` | 4.5:1 | 7.8 | 6.3 |
| `Editor.InlayHint` | `Brush.Window` | 4.5:1 | 5.4 | 4.9 |
| `Brush.Terminal.Foreground` | `Brush.Terminal.Background` | 4.5:1 | 12.7 | 16.2 |
| `Editor.ErrorSquiggle` | `Brush.Window` | 3:1 | 5.9 | 5.0 |

## Surfaces

Surfaces, from the window's edge inwards. Light follows Visual Studio's Light: white documents in cool gray chrome.

| Token | Dark | Light |
|---|---|---|
| `Brush.MenuBar` | `#15171C` | `#E9ECF1` |
| `Brush.Window` | `#1B1D23` | `#FFFFFF` |
| `Brush.Toolbar` | `#1F2128` | `#F3F5F8` |
| `Brush.Panel` | `#22252C` | `#F7F8FA` |
| `Brush.CurrentLine` | `#23262F` | `#F2F5FA` |
| `Brush.Popup` | `#262A33` | `#FFFFFF` |
| `Brush.Control` | `#272A32` | `#FFFFFF` |
| `Brush.Hover` | `#2A2E37` | `#E6E9EF` |
| `Brush.Pressed` | `#33384A` | `#D8DDE6` |
| `Brush.Selection` | `#2B3B57` | `#CCE2F7` |
| `Brush.SelectionInactive` | `#262C38` | `#E3E7ED` |

## Lines

| Token | Dark | Light |
|---|---|---|
| `Brush.Border` | `#2A2E37` | `#D9DDE4` |
| `Brush.Divider` | `#2F333D` | `#E1E4EA` |
| `Brush.BorderStrong` | `#343844` | `#C3C9D3` |
| `Brush.BorderPopup` | `#3A3F4B` | `#BEC5D0` |

## Text

Text. TextDisabled is for disabled controls only; everything else passes WCAG AA (4.5:1) on every surface.

| Token | Dark | Light |
|---|---|---|
| `Brush.TextBright` | `#E8ECF2` | `#111317` |
| `Brush.Text` | `#D7DAE0` | `#1D2127` |
| `Brush.TextSecondary` | `#C5CAD3` | `#343A44` |
| `Brush.TextLabel` | `#B7BDC8` | `#454C58` |
| `Brush.TextMuted` | `#A0A7B3` | `#5A6170` |
| `Brush.LineNumber` | `#858C99` | `#6A7180` |
| `Brush.TextDisabled` | `#6E7685` | `#9EA4AF` |
| `Brush.Link` | `#8FB8FF` | `#0A5BB8` |
| `Brush.OnAccent` | `#FFFFFF` | `#FFFFFF` |
| `Brush.SelectionText` | `#FFFFFF` | `#111317` |

## Accent

Accent: TabStudio's teal, the same in both themes, with white text on it.

| Token | Dark | Light |
|---|---|---|
| `Brush.Accent` | `#0F7A70` | `#0F7A70` |
| `Brush.AccentHover` | `#0E7F74` | `#0D6C63` |
| `Brush.AccentPressed` | `#0C655D` | `#0A5A53` |
| `Brush.AccentSoft` | `#1E3A36` | `#D7EEEA` |
| `Brush.AccentText` | `#6FD6C4` | `#0A6A61` |

## Status

| Token | Dark | Light |
|---|---|---|
| `Brush.Error` | `#F47067` | `#C42B2B` |
| `Brush.Warning` | `#E3B341` | `#8A5E00` |
| `Brush.Info` | `#6CB6FF` | `#1462BE` |
| `Brush.Success` | `#6FD6A0` | `#157536` |
| `Brush.SuccessSoft` | `#1C2E28` | `#DCF1E3` |
| `Brush.Breakpoint` | `#E5534B` | `#D13438` |
| `Brush.Running` | `#F0883E` | `#B24C00` |
| `Brush.Destructive` | `#C93C37` | `#C42B2B` |
| `Brush.DestructiveHover` | `#B8332E` | `#A82222` |

## Solution Explorer

| Token | Dark | Light |
|---|---|---|
| `Brush.Solution` | `#C9B6FF` | `#6A3DB8` |
| `Brush.ProjectApp` | `#6CB6FF` | `#1462BE` |
| `Brush.ProjectLibrary` | `#6FD6C4` | `#0A7368` |
| `Brush.ProjectTest` | `#E6A26B` | `#A65300` |
| `Brush.Folder` | `#D9B65C` | `#A87A00` |
| `Brush.Pill` | `#2A2E37` | `#E5E8EE` |

## File badges

| Token | Dark | Light |
|---|---|---|
| `Brush.Badge.CSharp` | `#C9B6FF` | `#5B36A8` |
| `Brush.Badge.CSharpBackground` | `#2E2846` | `#ECE5FA` |
| `Brush.Badge.Data` | `#E6C46B` | `#835D00` |
| `Brush.Badge.Markup` | `#E6A26B` | `#A04F00` |
| `Brush.Badge.Script` | `#E2D58E` | `#6F6300` |
| `Brush.Badge.Text` | `#9AA1AE` | `#5A6170` |
| `Brush.Badge.Notebook` | `#F2A65A` | `#A44E00` |
| `Brush.Badge.NotebookBackground` | `#3A2B1E` | `#FBEBDC` |

## Chrome

Chrome: scrims, the status bar's controls, notices, the activity bar, badges and the debugger's colors

| Token | Dark | Light |
|---|---|---|
| `Brush.Scrim` | `#99000000` | `#66141A22` |
| `Brush.StatusBar.Hover` | `#26FFFFFF` | `#26FFFFFF` |
| `Brush.StatusBar.Pressed` | `#40FFFFFF` | `#40FFFFFF` |
| `Brush.StatusBar.Well` | `#59000000` | `#59000000` |
| `Brush.OnAccent.Divider` | `#59FFFFFF` | `#59FFFFFF` |
| `Brush.OnAccent.Pill` | `#33000000` | `#33000000` |
| `Brush.Notice.SuccessBorder` | `#2D4A3E` | `#A6D9B8` |
| `Brush.Notice.Error` | `#3A2224` | `#FCE9E9` |
| `Brush.Notice.ErrorBorder` | `#5A3134` | `#F0B4B4` |
| `Brush.Notice.Warning` | `#33291A` | `#FFF5DB` |
| `Brush.Notice.WarningBorder` | `#5A4520` | `#EBCB80` |
| `Brush.Notice.Info` | `#1D2B3D` | `#E5F0FC` |
| `Brush.Notice.InfoBorder` | `#2E4A6B` | `#A9C9EE` |
| `Brush.ActivityBar` | `#191B21` | `#ECEEF2` |
| `Brush.ActivityIndicator` | `#6FD6C4` | `#0F7A70` |
| `Brush.Badge.Count` | `#D0372F` | `#C42B2B` |
| `Brush.Debugging` | `#B85212` | `#B04A0C` |
| `Brush.CurrentStatement` | `#F2CC4E` | `#A87E00` |
| `Brush.CallerStatement` | `#6FD6A0` | `#1F9152` |
| `Brush.ExceptionHeader` | `#3A2326` | `#FBE4E5` |

## The editor's own decorations

The editor's own decorations: selection, caret, squiggles, breakpoints, debugger lines, diffs, coverage and highlights

| Token | Dark | Light |
|---|---|---|
| `Editor.Selection` | `#552B4F86` | `#99ADD6FF` |
| `Editor.Caret` | `#E8ECF2` | `#111317` |
| `Editor.ErrorSquiggle` | `#F47067` | `#D3302F` |
| `Editor.WarningSquiggle` | `#E3B341` | `#B07D00` |
| `Editor.MessageSquiggle` | `#6CB6FF` | `#1462BE` |
| `Editor.Breakpoint` | `#E5534B` | `#D13438` |
| `Editor.BreakpointGlyph` | `#FFFFFF` | `#FFFFFF` |
| `Editor.CurrentStatement` | `#F2CC4E` | `#F2C230` |
| `Editor.CallerStatement` | `#6FD6A0` | `#2EA05A` |
| `Editor.ArrowOutline` | `#1B1D23` | `#6B5300` |
| `Editor.BreakpointLine` | `#40E5534B` | `#33D13438` |
| `Editor.CurrentStatementLine` | `#4DF2CC4E` | `#80FFE36B` |
| `Editor.CurrentStatementBorder` | `#B3F2CC4E` | `#E0B000` |
| `Editor.CallerLine` | `#406FD6A0` | `#4D7DDB9F` |
| `Editor.CallerBorder` | `#B36FD6A0` | `#2EA05A` |
| `Editor.InlayHint` | `#8B93A1` | `#6A7180` |
| `Editor.ReferenceHighlight` | `#8C34496B` | `#B3D6E6F9` |
| `Editor.DefinitionHighlight` | `#8C3D5A48` | `#B3D2EEDC` |
| `Editor.DiffAddedLine` | `#1E3A2B` | `#E3F8E8` |
| `Editor.DiffRemovedLine` | `#45242A` | `#FDE8E8` |
| `Editor.DiffCollapsedLine` | `#23262F` | `#F2F4F8` |
| `Editor.DiffAddedText` | `#6FD6A0` | `#177A39` |
| `Editor.DiffRemovedText` | `#F47067` | `#C42B2B` |
| `Editor.DiffLineNumber` | `#858C99` | `#6A7180` |
| `Editor.DiffCollapsedText` | `#8FB8FF` | `#0A5BB8` |
| `Editor.Covered` | `#6FD6A0` | `#1E8A44` |
| `Editor.Failing` | `#F47067` | `#D3302F` |
| `Editor.NotCovered` | `#6CB6FF` | `#1462BE` |
| `Editor.ReviewMarker` | `#0F7A70` | `#0F7A70` |
| `Editor.ReviewMarkerGlyph` | `#FFFFFF` | `#FFFFFF` |
| `Editor.SnippetTabStop` | `#337C7C7C` | `#1F7C7C7C` |
| `Editor.SnippetTabStopBorder` | `#B37C9CC4` | `#B35A7FB0` |
| `Editor.BracketMatch` | `#1A0064FF` | `#1A0064FF` |
| `Editor.BracketMatchBorder` | `#8A8F99` | `#AEB4BF` |
| `Editor.FoldMarker` | `#9AA1AE` | `#6A7180` |
| `Editor.FoldMarkerBackground` | `#1B1D23` | `#FFFFFF` |
| `Editor.FoldMarkerSelected` | `#FFFFFF` | `#111317` |
| `Editor.PeekDefinitionLine` | `#2B3346` | `#E3EDFA` |

## TabStudio's syntax colors

TabStudio's syntax colors, for C# (Roslyn) and every TextMate grammar; the light ones are the same hues, darkened for white

| Token | Dark | Light |
|---|---|---|
| `Syntax.Text` | `#D7DAE0` | `#1D2127` |
| `Syntax.Keyword` | `#7FB2FF` | `#0B57C4` |
| `Syntax.ControlKeyword` | `#D3A6F2` | `#8F2FB0` |
| `Syntax.Type` | `#5FD0BA` | `#16786B` |
| `Syntax.Method` | `#E2D58E` | `#795300` |
| `Syntax.Variable` | `#A9D4F5` | `#1C4F7E` |
| `Syntax.String` | `#E6A26B` | `#A3410B` |
| `Syntax.StringEscape` | `#F0C674` | `#8A5A00` |
| `Syntax.Number` | `#B5CEA8` | `#2F7A2B` |
| `Syntax.Comment` | `#8CA77A` | `#4C7A3A` |
| `Syntax.Muted` | `#9AA1AE` | `#5A6170` |
| `Syntax.Excluded` | `#6E7685` | `#9EA4AF` |

## Output and the terminal

| Token | Dark | Light |
|---|---|---|
| `Brush.Output.Error` | `#F47067` | `#C42B2B` |
| `Brush.Output.Warning` | `#E3B341` | `#8F6200` |
| `Brush.Output.Input` | `#6FD6C4` | `#0A6A61` |
| `Brush.Output.Info` | `#9AA1AE` | `#5A6170` |
| `Brush.Output.Success` | `#6FD6A0` | `#177A39` |
| `Brush.Terminal.Background` | `#16181D` | `#FFFFFF` |
| `Brush.Terminal.Foreground` | `#D7DAE0` | `#1D2127` |
| `Brush.Terminal.Cursor` | `#6FD6C4` | `#0F7A70` |
| `Brush.Terminal.Selection` | `#2B4A6F` | `#BFDBF7` |
| `Brush.Terminal.ScrollThumb` | `#4A5263` | `#C3C9D3` |
| `Terminal.Ansi0` | `#1B1D23` | `#1D2127` |
| `Terminal.Ansi1` | `#F47067` | `#C42B2B` |
| `Terminal.Ansi2` | `#6FD6A0` | `#177A39` |
| `Terminal.Ansi3` | `#E3B341` | `#8A5E00` |
| `Terminal.Ansi4` | `#6CB6FF` | `#1462BE` |
| `Terminal.Ansi5` | `#DCBDFB` | `#8250DF` |
| `Terminal.Ansi6` | `#56D4DD` | `#1B7C83` |
| `Terminal.Ansi7` | `#C5CAD3` | `#5A6170` |
| `Terminal.Ansi8` | `#6E7685` | `#6A7180` |
| `Terminal.Ansi9` | `#FF938A` | `#D3302F` |
| `Terminal.Ansi10` | `#8DDB8C` | `#1A7F3C` |
| `Terminal.Ansi11` | `#F5D06F` | `#9A6700` |
| `Terminal.Ansi12` | `#96D0FF` | `#0969DA` |
| `Terminal.Ansi13` | `#E8D3FF` | `#9A4FD8` |
| `Terminal.Ansi14` | `#8BE9F0` | `#0E7F88` |
| `Terminal.Ansi15` | `#F0F3F6` | `#343A44` |

## Notebooks

| Token | Dark | Light |
|---|---|---|
| `Brush.Notebook.Cell` | `#22252C` | `#F7F8FA` |
| `Brush.Notebook.CodeBlock` | `#262A33` | `#F0F2F6` |
| `Brush.Notebook.ErrorBackground` | `#2B1F22` | `#FCEBEC` |
| `Brush.Notebook.TableHeader` | `#262A33` | `#EDF0F4` |
| `Brush.Notebook.TableStripe` | `#1F2128` | `#F8F9FB` |

## GitHub Actions

GitHub Actions: run states, and job logs in GitHub's palette

| Token | Dark | Light |
|---|---|---|
| `Brush.Run.Passed` | `#6FD6A0` | `#1A7F37` |
| `Brush.Run.Failed` | `#F47067` | `#CF222E` |
| `Brush.Run.Running` | `#E3B341` | `#8F6200` |
| `Brush.Run.Queued` | `#C69026` | `#8F6200` |
| `Brush.Run.Muted` | `#9AA1AE` | `#5A6170` |
| `Brush.Run.FailedSoft` | `#3A2326` | `#FFEBE9` |
| `Brush.Run.PassedSoft` | `#1C2E28` | `#DAFBE1` |
| `Brush.Run.RunningSoft` | `#352E1C` | `#FFF5D1` |
| `Brush.Log.Error` | `#F47067` | `#CF222E` |
| `Brush.Log.ErrorBackground` | `#33F47067` | `#1FCF222E` |
| `Brush.Log.Warning` | `#E3B341` | `#8F6200` |
| `Brush.Log.WarningBackground` | `#26E3B341` | `#26D4A72C` |
| `Brush.Log.Notice` | `#6CB6FF` | `#0969DA` |
| `Brush.Log.Debug` | `#8B949E` | `#59636E` |
| `Brush.Log.Command` | `#8FB8FF` | `#0550AE` |
| `Brush.Log.Step` | `#E8ECF2` | `#1F2328` |
| `Brush.Log.StepBackground` | `#262A33` | `#F0F2F5` |
| `Brush.Log.Group` | `#C5CAD3` | `#424A53` |
| `Brush.Log.Omitted` | `#9AA1AE` | `#59636E` |
| `Brush.Ansi.Gray` | `#8B949E` | `#59636E` |
| `Brush.Ansi.Red` | `#FF7B72` | `#CF222E` |
| `Brush.Ansi.Green` | `#7EE787` | `#116329` |
| `Brush.Ansi.Yellow` | `#E3B341` | `#7D4E00` |
| `Brush.Ansi.Blue` | `#79C0FF` | `#0969DA` |
| `Brush.Ansi.Magenta` | `#D2A8FF` | `#8250DF` |
| `Brush.Ansi.Cyan` | `#56D4DD` | `#1B7C83` |
| `Brush.Ansi.White` | `#F0F6FC` | `#424A53` |

## Pull requests and review cards

Pull requests and review cards

| Token | Dark | Light |
|---|---|---|
| `Brush.PullRequest.Open` | `#238636` | `#1F883D` |
| `Brush.PullRequest.Draft` | `#4A5060` | `#6E7781` |
| `Brush.PullRequest.Merged` | `#8957E5` | `#8250DF` |
| `Brush.PullRequest.Closed` | `#C93C37` | `#CF222E` |
| `Brush.PullRequest.OpenText` | `#6FD6A0` | `#1A7F37` |
| `Brush.PullRequest.MergedText` | `#C9B6FF` | `#6E3FC9` |
| `Brush.Review.Thread` | `#6CB6FF` | `#0969DA` |
| `Brush.Review.Pending` | `#E3B341` | `#8F6200` |
| `Brush.Review.Claude` | `#C9B6FF` | `#6E3FC9` |
| `Brush.Review.ClaudeSoft` | `#2E2846` | `#F1EBFF` |
| `Brush.Review.ClaudeBorder` | `#4D3F6E` | `#CDB9F5` |
| `Brush.Review.PendingSoft` | `#3A3220` | `#FFF5D1` |
| `Brush.Review.Card` | `#262A33` | `#FFFFFF` |

## Extensions

| Token | Dark | Light |
|---|---|---|
| `Brush.Extensions.ListBackground` | `#1F2128` | `#F3F5F8` |
| `Brush.Extensions.Card` | `#22252C` | `#FFFFFF` |
| `Brush.Extensions.Star` | `#E3B341` | `#9A6700` |
| `Brush.Extensions.Verified` | `#6CB6FF` | `#0969DA` |
| `Brush.Extensions.Scrim` | `#80000000` | `#59141A22` |
| `Brush.Extensions.OnBadge` | `#F2F4F8` | `#FFFFFF` |

## The Claude agent

| Token | Dark | Light |
|---|---|---|
| `Brush.Agent.UserBubble` | `#26324A` | `#E2ECFA` |
| `Brush.Agent.DiffAdded` | `#1E3A2A` | `#E3F8E8` |
| `Brush.Agent.DiffRemoved` | `#43232A` | `#FDE8E8` |

## The Toolchain Store

| Token | Dark | Light |
|---|---|---|
| `Brush.Toolchains.Ready` | `#1A2B2A` | `#E0F2EF` |
| `Brush.Toolchains.Attention` | `#2E2A1B` | `#FFF5D9` |

## Windows Forms apps in the app preview

The desktop a running Windows Forms app's windows sit on: Windows 11's dark wallpaper in the dark theme, its light one in
the light theme, as Windows does in dark mode. A gradient from the top left to the bottom right. The windows on it keep
the light frames Windows gives apps that do not ask for dark title bars, and the designer draws its selection in
`Color.Accent` and its component tray in `Brush.Text` on `Brush.Pill`.

| Token | Dark | Light |
|---|---|---|
| `Brush.FormsDesktop` | `#22324A` → `#18253A` → `#101A2B` | `#DCE8F6` → `#C2D5EE` → `#A8C3E6` |

## IntelliSense symbol icons

| Token | Dark | Light |
|---|---|---|
| `Symbol.Purple` | `#B180D7` | `#6C3FB5` |
| `Symbol.Blue` | `#4DA3F2` | `#0E63C4` |
| `Symbol.LightBlue` | `#75BEFF` | `#1C78CF` |
| `Symbol.Gold` | `#E3B341` | `#9A6700` |
| `Symbol.Silver` | `#C5CAD3` | `#59606C` |
| `Symbol.Gray` | `#9AA1AE` | `#6A7180` |
| `Symbol.Ink` | `#1B1D23` | `#FFFFFF` |

## The same in both themes

Colors that are content rather than chrome: a QR code, the sketch pad's paper, the desktop pointer, avatars and the
Toolchain Store's language tiles (white text on each). And the states the status bar shows on its dark wells, as the
status bar is teal in both themes: on `Brush.StatusBar.Well` over `Brush.Accent` each has at least 3:1, as icons need.

| Token | Color |
|---|---|
| `Brush.QrCode.Light` | `#FFFFFF` |
| `Brush.QrCode.Dark` | `#000000` |
| `Brush.Sketch.Paper` | `#FFFFFF` |
| `Brush.Sketch.Ink` | `#000000` |
| `Brush.Pointer.Fill` | `#FFFFFF` |
| `Brush.Pointer.Outline` | `#000000` |
| `Brush.Avatar.1` | `#2E6F9E` |
| `Brush.Avatar.2` | `#7A4FA0` |
| `Brush.Avatar.3` | `#2F7D5B` |
| `Brush.Avatar.4` | `#A0563A` |
| `Brush.Avatar.5` | `#8A6D1F` |
| `Brush.Avatar.6` | `#9C3F5E` |
| `Brush.Avatar.7` | `#3F6A8A` |
| `Brush.Avatar.8` | `#566B2E` |
| `Brush.Toolchain.Python` | `#3D7AB8` |
| `Brush.Toolchain.Node` | `#3E8A3A` |
| `Brush.Toolchain.Cpp` | `#5B6FB5` |
| `Brush.Toolchain.Rust` | `#B7593A` |
| `Brush.Toolchain.Go` | `#2A8EA8` |
| `Brush.Toolchain.Java` | `#B5612C` |
| `Brush.Toolchain.Kotlin` | `#7F52D9` |
| `Brush.Toolchain.Php` | `#5E6199` |
| `Brush.Toolchain.Swift` | `#B83E1F` |
| `Brush.Toolchain.Windows` | `#2B79C2` |
| `Brush.Toolchain.Android` | `#2F7A45` |
| `Brush.Toolchain.Flutter` | `#0D61B8` |
| `Brush.Toolchain.Microchip` | `#A6303A` |
| `Brush.Toolchain.Other` | `#4A5263` |
| `Brush.StatusBar.Passed` | `#6FD6A0` |
| `Brush.StatusBar.Failed` | `#F47067` |
| `Brush.StatusBar.Running` | `#E3B341` |
| `Brush.StatusBar.Queued` | `#C69026` |
| `Brush.StatusBar.Muted` | `#AEB4BF` |
