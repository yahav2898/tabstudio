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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 1.61 | 8.70 | 99% | 0.34 | 1.32 | 2.98 | 112.4 → 113.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 1.85 | 10.18 | 99% | 0.50 | 1.40 | 3.29 | 151.3 → 149.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.58 | 1.72 | 13.29 | 99% | 0.58 | 1.34 | 3.22 | 187.4 → 171.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.48 | 10.38 | 99% | 0.48 | 1.49 | 3.16 | 130.2 → 128.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.91 | 12.16 | 100% | 0.50 | 1.59 | 3.52 | 107.7 → 99.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.44 | 1.45 | 8.03 | 100% | 0.39 | 1.12 | 3.07 | 132.5 → 124.3 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.40 | 1.34 | 12.95 | 99% | 0.38 | 1.10 | 2.80 | 132.6 → 124.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.47 | 1.45 | 9.39 | 99% | 0.39 | 1.17 | 2.76 | 131.9 → 123.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.47 | 1.50 | 8.83 | 99% | 0.41 | 1.21 | 3.05 | 109.3 → 104.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.78 | 14.47 | 99% | 0.56 | 1.63 | 3.70 | 135.9 → 128.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.76 | 11.06 | 99% | 0.55 | 1.74 | 3.48 | 136.3 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.43 | 7.82 | 100% | 0.45 | 1.13 | 2.44 | 71.0 → 67.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.256 |
| AttachingStyle - | 0.051 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
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
| touch drag and flick (real touch input) | 46 | 0.38 | 1.26 | 1.41 | 100% | 0.29 | 1.03 | 2.92 | 76.4 → 98.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 7 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 1.56 | 8.00 | 100% | 0.43 | 1.26 | 3.08 | 131.3 → 128.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.28 | 4.60 | 19.01 | 98% | 2.45 | 3.96 | 4.02 | 346.2 → 338.7 | 7 | 1.1 / 4 |

