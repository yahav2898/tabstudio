# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.27 | 12.89 | 99% | 0.44 | 1.94 | 4.53 | 119.8 → 121.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 2.69 | 13.81 | 99% | 0.66 | 2.08 | 4.70 | 157.9 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 2.81 | 16.67 | 99% | 0.80 | 2.11 | 4.70 | 193.7 → 178.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.94 | 14.33 | 99% | 0.62 | 2.36 | 4.78 | 137.8 → 135.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 2.84 | 12.59 | 99% | 0.62 | 2.20 | 4.53 | 115.3 → 107.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 2.52 | 13.97 | 99% | 0.65 | 2.03 | 4.64 | 140.1 → 132.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.53 | 15.60 | 99% | 0.64 | 2.04 | 4.75 | 140.0 → 132.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.62 | 14.61 | 99% | 0.63 | 2.09 | 4.62 | 139.8 → 131.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.87 | 3.00 | 14.29 | 100% | 0.59 | 2.44 | 4.62 | 117.3 → 112.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 2.83 | 12.31 | 99% | 0.60 | 2.23 | 4.69 | 143.1 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 2.47 | 14.26 | 99% | 0.59 | 2.06 | 4.14 | 142.6 → 134.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.03 | 9.79 | 100% | 0.51 | 1.70 | 3.37 | 78.6 → 75.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| ArrangingLayoutable ScrollingTextLayer | 0.054 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 2.38 | 2.53 | 100% | 0.50 | 1.92 | 4.88 | 85.1 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 141 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.18 | 14.03 | 99% | 0.60 | 2.52 | 4.82 | 139.2 → 135.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.20 | 2.92 | 18.54 | 98% | 0.66 | 2.36 | 4.64 | 350.8 → 344.4 | 8 | 1.1 / 4 |

