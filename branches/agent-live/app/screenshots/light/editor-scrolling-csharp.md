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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 3.31 | 96.00 | 99% | 0.54 | 2.88 | 5.26 | 120.8 → 121.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 3.46 | 16.15 | 99% | 0.76 | 2.89 | 5.11 | 158.3 → 156.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.22 | 3.65 | 14.99 | 99% | 0.90 | 2.87 | 5.06 | 194.1 → 177.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.12 | 3.77 | 16.31 | 99% | 0.73 | 3.00 | 5.21 | 137.9 → 136.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 2.43 | 10.42 | 99% | 0.60 | 1.96 | 4.64 | 113.8 → 107.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.80 | 13.06 | 99% | 0.57 | 2.47 | 4.54 | 139.3 → 131.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 3.11 | 13.74 | 99% | 0.61 | 2.63 | 4.80 | 139.3 → 131.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 4.13 | 16.23 | 99% | 0.67 | 3.08 | 5.16 | 140.3 → 131.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.04 | 11.86 | 99% | 0.58 | 2.58 | 4.48 | 117.2 → 112.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.04 | 3.51 | 13.53 | 99% | 0.71 | 2.91 | 5.06 | 143.5 → 135.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 2.66 | 12.31 | 99% | 0.59 | 2.15 | 4.19 | 141.8 → 134.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.97 | 11.71 | 100% | 0.58 | 2.56 | 3.81 | 79.1 → 75.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.216 |
| ArrangingLayoutable ScrollingTextLayer | 0.046 |
| ArrangingLayoutable NumberDrawing | 0.042 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.67 | 2.53 | 3.18 | 100% | 0.43 | 2.07 | 4.59 | 84.9 → 106.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 236 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.33 | 12.70 | 99% | 0.68 | 2.81 | 4.95 | 139.0 → 136.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 5.97 | 89.02 | 97% | 2.65 | 3.51 | 5.33 | 351.0 → 344.5 | 9 | 1.1 / 4 |

