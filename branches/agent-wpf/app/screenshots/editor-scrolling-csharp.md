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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.05 | 12.40 | 99% | 0.37 | 1.73 | 2.85 | 112.7 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.82 | 11.29 | 99% | 0.54 | 1.98 | 3.03 | 151.3 → 149.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 4.25 | 79.08 | 99% | 0.81 | 2.57 | 3.32 | 188.4 → 171.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.17 | 11.75 | 99% | 0.44 | 1.76 | 2.87 | 130.6 → 127.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 1.92 | 9.14 | 99% | 0.42 | 1.62 | 2.65 | 107.1 → 100.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 1.93 | 12.51 | 100% | 0.46 | 1.72 | 2.79 | 132.9 → 125.0 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 2.02 | 10.94 | 99% | 0.46 | 1.69 | 2.88 | 133.0 → 125.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.86 | 12.68 | 99% | 0.57 | 2.28 | 3.01 | 132.7 → 124.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.27 | 8.99 | 100% | 0.41 | 1.85 | 3.00 | 110.1 → 104.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 2.13 | 13.08 | 99% | 0.44 | 1.73 | 2.88 | 135.5 → 128.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 1.82 | 9.77 | 99% | 0.46 | 1.54 | 2.57 | 135.3 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.34 | 7.74 | 100% | 0.39 | 1.07 | 2.03 | 70.6 → 67.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.177 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 1.79 | 1.93 | 100% | 0.41 | 1.34 | 3.03 | 76.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 9 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 1.81 | 10.13 | 100% | 0.44 | 1.41 | 3.03 | 131.3 → 128.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.90 | 2.05 | 14.44 | 98% | 0.51 | 1.59 | 3.01 | 344.6 → 338.0 | 7 | 1.1 / 4 |

