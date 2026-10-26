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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.83 | 2.23 | 10.44 | 99% | 0.50 | 1.77 | 3.57 | 113.0 → 115.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.33 | 11.13 | 99% | 0.60 | 1.74 | 3.39 | 151.8 → 149.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 2.60 | 12.98 | 99% | 0.74 | 1.88 | 3.65 | 188.4 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 3.47 | 13.57 | 99% | 0.73 | 2.66 | 3.71 | 132.0 → 128.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.03 | 10.36 | 100% | 0.56 | 1.52 | 3.39 | 107.5 → 100.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.95 | 2.40 | 14.53 | 99% | 0.65 | 1.79 | 3.82 | 133.9 → 125.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 2.31 | 11.75 | 99% | 0.60 | 1.76 | 3.52 | 133.6 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 2.43 | 12.99 | 99% | 0.64 | 1.77 | 3.61 | 133.0 → 124.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 3.19 | 11.91 | 100% | 0.72 | 2.51 | 3.69 | 111.0 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 1.86 | 10.37 | 99% | 0.53 | 1.46 | 3.10 | 135.6 → 128.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 2.16 | 13.99 | 99% | 0.59 | 1.64 | 3.04 | 136.2 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 1.80 | 7.98 | 100% | 0.52 | 1.39 | 2.63 | 71.7 → 67.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.276 |
| AttachingStyle - | 0.080 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 2.14 | 2.35 | 100% | 0.49 | 1.67 | 3.67 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 19 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.27 | 10.65 | 99% | 0.52 | 1.65 | 3.49 | 131.9 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 2.61 | 15.21 | 98% | 0.62 | 2.02 | 3.66 | 345.6 → 338.6 | 8 | 1.1 / 4 |

