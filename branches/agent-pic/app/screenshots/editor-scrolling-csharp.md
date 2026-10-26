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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.05 | 8.21 | 100% | 0.38 | 1.70 | 4.31 | 113.7 → 115.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.23 | 67.68 | 99% | 0.69 | 2.46 | 4.78 | 152.6 → 150.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.41 | 14.07 | 99% | 0.71 | 1.81 | 4.45 | 188.2 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.15 | 9.91 | 100% | 0.52 | 1.74 | 4.46 | 131.7 → 128.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 1.76 | 10.29 | 99% | 0.48 | 1.44 | 3.86 | 108.0 → 101.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.27 | 68.57 | 99% | 0.61 | 2.55 | 4.70 | 134.4 → 126.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 2.12 | 10.84 | 99% | 0.51 | 1.72 | 4.24 | 133.9 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.12 | 10.22 | 99% | 0.50 | 1.71 | 4.15 | 133.2 → 125.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.32 | 8.77 | 100% | 0.48 | 1.78 | 4.20 | 110.8 → 106.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.74 | 13.05 | 99% | 0.57 | 1.99 | 4.90 | 137.2 → 129.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.18 | 12.05 | 99% | 0.56 | 1.79 | 4.05 | 136.8 → 128.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 1.83 | 10.69 | 100% | 0.47 | 1.48 | 3.36 | 71.7 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.243 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.01 | 3.10 | 15.27 | 98% | 0.65 | 2.09 | 5.45 | 78.4 → 101.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 264 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 1.97 | 8.97 | 99% | 0.47 | 1.58 | 4.16 | 132.4 → 129.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.55 | 13.82 | 98% | 0.59 | 1.96 | 4.18 | 345.6 → 339.1 | 8 | 1.1 / 4 |

