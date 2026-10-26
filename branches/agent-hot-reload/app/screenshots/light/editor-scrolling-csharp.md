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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.07 | 9.79 | 99% | 0.43 | 1.67 | 4.08 | 113.2 → 114.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 2.23 | 12.25 | 99% | 0.61 | 1.72 | 4.09 | 151.3 → 149.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 4.05 | 18.61 | 99% | 0.92 | 2.70 | 4.75 | 187.2 → 172.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 2.69 | 13.33 | 99% | 0.61 | 1.98 | 4.55 | 131.2 → 128.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 2.24 | 12.06 | 99% | 0.60 | 1.73 | 4.33 | 107.6 → 100.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 2.15 | 15.32 | 99% | 0.58 | 1.69 | 4.29 | 133.4 → 125.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 2.67 | 13.49 | 99% | 0.67 | 1.92 | 4.48 | 133.3 → 125.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.98 | 3.07 | 16.28 | 99% | 0.67 | 2.04 | 4.43 | 133.2 → 124.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 2.17 | 12.24 | 99% | 0.55 | 1.71 | 4.06 | 110.7 → 105.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 2.18 | 12.20 | 99% | 0.55 | 1.72 | 4.27 | 135.9 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.33 | 12.21 | 99% | 0.62 | 1.80 | 3.96 | 135.7 → 128.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.63 | 10.51 | 99% | 0.62 | 1.53 | 3.52 | 71.8 → 68.6 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| ArrangingLayoutable TextView | 0.066 |
| AttachingStyle - | 0.021 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.89 | 2.20 | 2.79 | 100% | 0.60 | 1.67 | 4.56 | 78.2 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 64 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.23 | 15.01 | 99% | 0.60 | 1.71 | 4.24 | 131.5 → 128.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.31 | 4.82 | 21.33 | 98% | 0.85 | 4.06 | 4.56 | 343.9 → 338.5 | 9 | 1.1 / 4 |

