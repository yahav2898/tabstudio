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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.38 | 10.67 | 99% | 0.40 | 2.05 | 3.82 | 111.5 → 112.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.64 | 11.23 | 100% | 0.57 | 2.11 | 4.04 | 150.0 → 147.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 4.41 | 14.55 | 99% | 0.86 | 2.49 | 4.24 | 186.6 → 170.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.38 | 11.16 | 99% | 0.48 | 1.97 | 3.73 | 129.4 → 126.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.13 | 8.12 | 100% | 0.50 | 1.79 | 3.62 | 105.8 → 98.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.09 | 12.34 | 99% | 0.48 | 1.68 | 3.79 | 131.9 → 123.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.19 | 11.85 | 99% | 0.73 | 2.46 | 4.08 | 131.7 → 123.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.52 | 2.01 | 10.11 | 99% | 0.44 | 1.72 | 3.43 | 130.4 → 122.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.48 | 1.94 | 8.76 | 100% | 0.38 | 1.70 | 3.31 | 107.8 → 102.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 2.01 | 10.10 | 99% | 0.45 | 1.74 | 3.50 | 133.8 → 126.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 2.57 | 10.17 | 99% | 0.49 | 1.97 | 3.28 | 133.8 → 126.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.12 | 63.99 | 99% | 0.45 | 1.79 | 2.94 | 69.9 → 65.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.150 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.46 | 1.88 | 2.16 | 100% | 0.32 | 1.63 | 3.56 | 74.3 → 98.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 5 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 1.98 | 12.86 | 99% | 0.48 | 1.64 | 3.77 | 129.9 → 127.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 4.71 | 66.43 | 98% | 0.86 | 4.02 | 4.27 | 343.7 → 336.7 | 8 | 1.1 / 4 |

