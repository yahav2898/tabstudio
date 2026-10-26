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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 2.39 | 11.78 | 99% | 0.50 | 1.91 | 4.16 | 113.0 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 2.74 | 14.08 | 99% | 0.70 | 2.12 | 4.49 | 150.4 → 148.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.48 | 15.22 | 100% | 0.74 | 1.87 | 4.16 | 185.2 → 169.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 3.04 | 15.65 | 99% | 0.64 | 2.34 | 4.24 | 130.3 → 127.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 2.64 | 10.05 | 99% | 0.61 | 1.87 | 4.02 | 107.3 → 100.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.30 | 16.25 | 99% | 0.57 | 1.81 | 4.04 | 132.8 → 124.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 2.51 | 12.60 | 99% | 0.63 | 2.00 | 4.29 | 132.4 → 124.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.26 | 13.30 | 99% | 0.60 | 1.78 | 3.92 | 131.7 → 123.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.73 | 2.82 | 10.70 | 99% | 0.53 | 1.86 | 3.82 | 109.7 → 105.0 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 3.22 | 12.28 | 99% | 0.71 | 2.60 | 4.66 | 135.7 → 127.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.84 | 2.33 | 13.18 | 99% | 0.59 | 1.88 | 3.62 | 135.0 → 127.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 1.89 | 8.02 | 100% | 0.49 | 1.49 | 2.90 | 71.3 → 67.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.69 | 1.95 | 2.08 | 100% | 0.43 | 1.59 | 3.86 | 77.1 → 99.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 11 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.08 | 14.47 | 99% | 0.61 | 2.35 | 4.26 | 131.3 → 127.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.20 | 2.92 | 36.04 | 98% | 0.72 | 2.26 | 4.38 | 342.1 → 335.2 | 7 | 1.1 / 4 |

