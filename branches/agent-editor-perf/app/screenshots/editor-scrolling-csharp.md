# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 2.25 | 12.44 | 99% | 0.44 | 1.78 | 4.51 | 125.9 → 125.2 | 6 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 2.50 | 13.53 | 99% | 0.65 | 1.84 | 4.62 | 151.7 → 149.4 | 8 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 3.39 | 12.47 | 99% | 0.86 | 1.87 | 4.83 | 179.0 → 169.8 | 6 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.49 | 9.51 | 99% | 0.58 | 1.76 | 4.50 | 139.0 → 134.5 | 6 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.00 | 10.89 | 99% | 0.61 | 1.51 | 4.33 | 71.6 → 64.6 | 5 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.49 | 8.24 | 99% | 0.62 | 1.72 | 4.50 | 141.0 → 131.7 | 7 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.31 | 11.25 | 99% | 0.61 | 1.80 | 4.67 | 140.9 → 131.8 | 6 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.22 | 13.32 | 99% | 0.56 | 1.70 | 4.32 | 140.2 → 131.0 | 7 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 2.21 | 12.27 | 100% | 0.50 | 1.75 | 4.47 | 118.2 → 111.8 | 6 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.49 | 11.02 | 99% | 0.60 | 1.78 | 4.62 | 143.5 → 134.5 | 6 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.73 | 11.45 | 99% | 0.60 | 1.86 | 4.22 | 143.7 → 134.4 | 7 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 1.80 | 7.24 | 100% | 0.51 | 1.40 | 3.48 | 36.1 → 32.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.267 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| RaisingRoutedEvent ScrollViewer | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.83 | 3.17 | 10.15 | 98% | 0.48 | 2.51 | 4.73 | 104.3 → 119.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 260 frames at 120 Hz; the text stood still on 0 of them, 260 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.49 | 9.02 | 99% | 0.60 | 1.81 | 4.66 | 140.1 → 135.1 | 7 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.22 | 4.66 | 18.77 | 98% | 0.73 | 4.06 | 4.93 | 309.4 → 301.3 | 11 | 1.1 / 4 |

