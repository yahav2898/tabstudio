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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 1.98 | 11.79 | 99% | 0.39 | 1.67 | 4.26 | 113.5 → 114.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.28 | 11.90 | 99% | 0.57 | 1.74 | 4.27 | 151.5 → 149.2 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 4.56 | 16.92 | 99% | 2.30 | 2.70 | 4.71 | 187.8 → 171.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.27 | 13.52 | 99% | 0.56 | 1.78 | 4.37 | 131.1 → 128.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 1.97 | 10.62 | 99% | 0.49 | 1.57 | 4.14 | 107.6 → 100.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.17 | 15.14 | 99% | 0.56 | 1.70 | 4.37 | 133.4 → 125.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.78 | 70.92 | 99% | 0.56 | 2.37 | 4.49 | 133.6 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 2.13 | 12.21 | 99% | 0.54 | 1.72 | 4.17 | 132.6 → 124.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 1.97 | 10.83 | 99% | 0.45 | 1.66 | 3.98 | 110.4 → 105.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.81 | 14.45 | 99% | 0.56 | 1.96 | 4.43 | 135.8 → 128.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.72 | 78.84 | 99% | 0.56 | 1.94 | 4.01 | 136.0 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.84 | 2.32 | 100% | 0.45 | 1.46 | 3.52 | 71.5 → 68.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.159 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 2.38 | 2.54 | 100% | 0.38 | 1.98 | 5.26 | 83.6 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 37 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.75 | 71.16 | 99% | 0.53 | 2.34 | 4.30 | 131.9 → 128.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 2.58 | 15.94 | 98% | 0.62 | 2.06 | 4.31 | 343.8 → 337.2 | 7 | 1.1 / 4 |

