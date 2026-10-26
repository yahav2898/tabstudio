# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.71 | 90.21 | 100% | 0.53 | 2.06 | 4.38 | 113.6 → 114.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.67 | 2.13 | 13.18 | 99% | 0.52 | 1.65 | 3.75 | 150.6 → 148.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.13 | 12.46 | 99% | 0.60 | 1.63 | 3.81 | 186.3 → 170.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.02 | 10.18 | 99% | 0.48 | 1.65 | 3.69 | 130.1 → 127.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.43 | 9.55 | 99% | 0.56 | 1.81 | 3.96 | 107.0 → 101.1 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 2.86 | 12.32 | 99% | 0.64 | 2.07 | 4.21 | 133.5 → 125.2 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.26 | 10.75 | 99% | 0.55 | 1.80 | 3.96 | 133.3 → 125.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.18 | 12.44 | 99% | 0.53 | 1.73 | 3.83 | 132.5 → 124.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.08 | 9.59 | 99% | 0.46 | 1.75 | 3.69 | 109.9 → 105.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 3.09 | 14.26 | 99% | 0.62 | 2.14 | 4.25 | 135.7 → 128.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 2.87 | 12.44 | 99% | 0.56 | 2.24 | 3.69 | 135.9 → 127.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.74 | 8.94 | 100% | 0.46 | 1.41 | 2.74 | 71.6 → 68.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.200 |
| ArrangingLayoutable ScrollingTextLayer | 0.040 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 2.11 | 2.41 | 100% | 0.41 | 1.61 | 4.04 | 77.3 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 16 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 2.78 | 11.23 | 99% | 0.62 | 2.09 | 4.29 | 131.4 → 129.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 4.47 | 15.90 | 98% | 0.61 | 2.78 | 4.05 | 343.8 → 336.9 | 8 | 1.1 / 4 |

