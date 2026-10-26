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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.57 | 11.90 | 99% | 0.51 | 2.02 | 4.77 | 113.3 → 114.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 2.71 | 11.02 | 99% | 0.71 | 2.08 | 4.80 | 151.8 → 149.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.27 | 4.58 | 13.21 | 100% | 1.02 | 2.76 | 5.23 | 188.2 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 2.70 | 10.73 | 99% | 0.66 | 2.06 | 4.75 | 131.2 → 128.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.39 | 8.55 | 99% | 0.65 | 1.86 | 4.54 | 107.6 → 100.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.73 | 12.01 | 99% | 0.68 | 2.06 | 4.89 | 133.5 → 126.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.03 | 3.11 | 13.87 | 99% | 0.71 | 2.22 | 5.18 | 134.3 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.94 | 2.68 | 11.75 | 99% | 0.71 | 2.05 | 4.80 | 132.7 → 124.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 2.40 | 8.32 | 99% | 0.56 | 1.88 | 4.55 | 110.5 → 105.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.07 | 3.33 | 8.38 | 100% | 0.75 | 2.55 | 5.23 | 136.9 → 129.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.92 | 2.47 | 12.06 | 99% | 0.64 | 1.91 | 4.44 | 136.3 → 128.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.09 | 7.07 | 100% | 0.56 | 1.62 | 3.73 | 72.5 → 68.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.269 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.86 | 2.82 | 3.29 | 100% | 0.49 | 2.14 | 4.99 | 78.2 → 100.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 225 frames at 120 Hz; the text stood still on 0 of them, 258 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.98 | 2.56 | 11.22 | 99% | 0.64 | 1.96 | 4.87 | 132.0 → 129.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.43 | 3.08 | 16.13 | 98% | 0.77 | 2.34 | 4.86 | 345.3 → 338.2 | 8 | 1.1 / 4 |

