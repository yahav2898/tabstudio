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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.58 | 10.26 | 99% | 0.51 | 2.09 | 4.78 | 113.5 → 114.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.06 | 2.73 | 12.68 | 99% | 0.72 | 2.09 | 4.77 | 151.7 → 149.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 4.99 | 76.71 | 98% | 2.62 | 3.02 | 5.00 | 188.3 → 172.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 2.76 | 11.13 | 99% | 0.65 | 2.16 | 4.95 | 131.2 → 129.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 2.67 | 15.42 | 99% | 0.69 | 2.06 | 4.86 | 108.1 → 101.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.04 | 4.56 | 15.25 | 99% | 0.80 | 2.54 | 4.99 | 133.9 → 126.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 2.96 | 12.65 | 99% | 0.74 | 2.24 | 5.00 | 134.3 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 2.85 | 13.17 | 99% | 0.74 | 2.19 | 4.89 | 133.1 → 125.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 2.75 | 10.98 | 99% | 0.62 | 2.13 | 4.74 | 110.9 → 106.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 3.12 | 11.93 | 99% | 0.69 | 2.57 | 5.04 | 137.4 → 128.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 2.59 | 11.63 | 99% | 0.67 | 2.09 | 4.38 | 136.0 → 128.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 2.31 | 10.52 | 100% | 0.60 | 1.81 | 3.78 | 72.7 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.289 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.98 | 3.20 | 4.58 | 100% | 0.58 | 2.49 | 5.25 | 78.3 → 101.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 220 frames at 120 Hz; the text stood still on 0 of them, 253 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.00 | 2.75 | 11.73 | 99% | 0.67 | 2.10 | 4.87 | 132.0 → 129.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 3.39 | 17.34 | 98% | 0.83 | 2.56 | 5.04 | 345.6 → 339.2 | 8 | 1.1 / 4 |

