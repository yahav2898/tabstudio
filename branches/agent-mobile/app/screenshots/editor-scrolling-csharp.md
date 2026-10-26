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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 1.97 | 10.19 | 99% | 0.48 | 1.59 | 3.23 | 112.0 → 113.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 3.34 | 15.17 | 99% | 0.79 | 2.60 | 3.63 | 151.0 → 148.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.18 | 12.93 | 99% | 0.66 | 1.61 | 3.27 | 185.9 → 170.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 1.97 | 10.86 | 99% | 0.52 | 1.55 | 3.27 | 130.0 → 127.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.67 | 10.51 | 100% | 0.49 | 1.29 | 2.82 | 106.5 → 99.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 1.82 | 13.43 | 99% | 0.52 | 1.42 | 2.98 | 132.1 → 124.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.31 | 11.35 | 99% | 0.63 | 1.70 | 3.40 | 132.2 → 124.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.13 | 11.95 | 99% | 0.65 | 2.35 | 3.52 | 132.0 → 123.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 1.93 | 8.50 | 100% | 0.49 | 1.49 | 3.01 | 109.4 → 104.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 1.88 | 11.24 | 99% | 0.53 | 1.49 | 3.18 | 134.7 → 127.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.51 | 1.63 | 10.29 | 99% | 0.44 | 1.27 | 2.52 | 134.2 → 126.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.34 | 7.07 | 100% | 0.38 | 1.09 | 2.10 | 70.6 → 67.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.160 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 1.60 | 2.07 | 100% | 0.37 | 1.30 | 3.06 | 76.2 → 98.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 260 frames at 120 Hz; the text stood still on 0 of them, 17 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.89 | 11.63 | 99% | 0.48 | 1.48 | 3.16 | 130.2 → 127.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.98 | 2.28 | 15.06 | 98% | 0.59 | 1.76 | 3.31 | 343.0 → 336.1 | 8 | 1.1 / 4 |

