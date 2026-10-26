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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 2.98 | 80.70 | 99% | 0.44 | 2.56 | 4.07 | 119.8 → 121.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.09 | 13.47 | 99% | 0.51 | 1.66 | 3.70 | 156.8 → 155.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.42 | 14.64 | 99% | 0.68 | 1.82 | 4.00 | 193.5 → 177.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.20 | 11.54 | 99% | 0.52 | 1.73 | 3.95 | 137.8 → 134.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.01 | 10.16 | 99% | 0.50 | 1.59 | 3.65 | 114.3 → 107.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 3.16 | 16.50 | 99% | 0.63 | 2.51 | 4.09 | 140.3 → 132.3 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.34 | 10.80 | 99% | 0.57 | 1.85 | 4.00 | 140.0 → 131.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.13 | 14.77 | 99% | 0.54 | 1.71 | 3.79 | 139.0 → 131.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.08 | 9.60 | 99% | 0.47 | 1.69 | 3.65 | 117.1 → 111.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.21 | 11.67 | 99% | 0.51 | 1.74 | 3.90 | 142.8 → 134.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 3.15 | 11.97 | 99% | 0.60 | 2.31 | 3.62 | 141.7 → 134.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.16 | 10.06 | 100% | 0.49 | 1.58 | 2.95 | 78.7 → 74.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 1.95 | 2.03 | 100% | 0.40 | 1.65 | 3.89 | 83.8 → 106.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 11 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.27 | 12.48 | 99% | 0.55 | 1.81 | 4.00 | 138.2 → 135.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.22 | 4.71 | 20.48 | 98% | 0.72 | 4.14 | 4.15 | 350.4 → 345.3 | 8 | 1.1 / 4 |

