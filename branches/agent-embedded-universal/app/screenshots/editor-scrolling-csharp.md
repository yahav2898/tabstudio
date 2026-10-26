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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 2.17 | 11.89 | 99% | 0.47 | 1.73 | 3.53 | 119.5 → 120.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 3.36 | 12.90 | 99% | 0.72 | 2.41 | 4.01 | 157.8 → 156.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 3.82 | 15.30 | 99% | 1.10 | 2.68 | 4.08 | 194.3 → 177.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 2.36 | 14.90 | 99% | 0.58 | 1.83 | 3.65 | 137.5 → 134.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 2.28 | 10.01 | 99% | 0.58 | 1.75 | 3.68 | 114.3 → 107.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.36 | 15.46 | 99% | 0.60 | 1.81 | 3.70 | 139.5 → 131.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 2.37 | 12.13 | 99% | 0.61 | 1.80 | 3.69 | 139.7 → 131.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.13 | 10.26 | 99% | 0.51 | 1.67 | 3.34 | 138.6 → 130.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.79 | 11.62 | 99% | 0.46 | 2.44 | 3.15 | 116.2 → 111.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.87 | 12.55 | 99% | 0.52 | 2.26 | 3.56 | 141.9 → 134.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 2.08 | 10.43 | 99% | 0.50 | 1.60 | 3.04 | 141.2 → 133.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 1.93 | 10.67 | 100% | 0.52 | 1.47 | 2.55 | 77.7 → 74.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.287 |
| ArrangingLayoutable Border | 0.041 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable LineDrawing | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 2.40 | 10.24 | 98% | 0.48 | 1.76 | 4.02 | 85.2 → 106.7 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 21 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 3.14 | 12.78 | 99% | 0.63 | 2.06 | 3.88 | 138.2 → 135.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 4.76 | 18.26 | 98% | 0.81 | 2.96 | 3.91 | 351.4 → 343.7 | 8 | 1.1 / 4 |

