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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.05 | 10.41 | 99% | 0.38 | 1.72 | 4.20 | 113.6 → 114.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 2.17 | 11.75 | 99% | 0.58 | 1.71 | 4.21 | 151.3 → 148.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.53 | 14.63 | 99% | 0.73 | 1.91 | 4.44 | 186.6 → 171.2 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.06 | 11.58 | 99% | 0.65 | 2.28 | 4.95 | 131.7 → 128.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.25 | 8.46 | 99% | 0.57 | 1.87 | 4.42 | 107.8 → 101.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.27 | 11.13 | 99% | 0.55 | 1.78 | 4.41 | 133.2 → 125.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.51 | 11.60 | 99% | 0.59 | 1.98 | 4.63 | 133.3 → 125.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.81 | 12.29 | 99% | 0.80 | 2.51 | 5.00 | 133.3 → 124.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.23 | 8.13 | 99% | 0.46 | 1.83 | 4.24 | 110.5 → 105.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 2.09 | 11.73 | 99% | 0.48 | 1.70 | 4.06 | 135.7 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 2.02 | 11.38 | 99% | 0.47 | 1.68 | 3.59 | 135.8 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.61 | 7.76 | 100% | 0.54 | 1.73 | 3.44 | 72.1 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.206 |
| ArrangingLayoutable TextView | 0.050 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 1.99 | 9.51 | 98% | 0.40 | 1.61 | 4.26 | 78.5 → 99.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 20 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 2.13 | 11.93 | 99% | 0.50 | 1.75 | 4.17 | 131.5 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.37 | 4.91 | 19.20 | 98% | 0.95 | 4.37 | 4.87 | 344.2 → 338.6 | 9 | 1.1 / 4 |

