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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.06 | 8.71 | 99% | 0.42 | 1.70 | 4.31 | 113.9 → 115.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.23 | 11.49 | 99% | 0.61 | 1.72 | 4.28 | 152.4 → 150.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 4.46 | 76.16 | 98% | 2.51 | 3.80 | 4.80 | 189.0 → 173.3 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.10 | 9.13 | 99% | 0.52 | 1.69 | 4.23 | 131.7 → 128.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 1.93 | 11.27 | 100% | 0.54 | 1.54 | 4.05 | 108.4 → 101.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.13 | 13.23 | 99% | 0.55 | 1.68 | 4.23 | 134.3 → 126.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 4.08 | 14.49 | 99% | 0.78 | 2.76 | 5.06 | 134.4 → 126.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.10 | 13.04 | 99% | 0.53 | 1.70 | 4.12 | 133.3 → 125.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.00 | 9.23 | 100% | 0.46 | 1.61 | 3.89 | 110.8 → 105.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.11 | 10.37 | 99% | 0.52 | 1.70 | 4.28 | 136.7 → 128.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.84 | 3.35 | 10.61 | 99% | 0.62 | 2.59 | 4.30 | 136.8 → 128.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.71 | 7.59 | 100% | 0.48 | 1.38 | 3.23 | 71.7 → 68.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.196 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 2.31 | 9.37 | 98% | 0.47 | 1.69 | 4.55 | 78.6 → 100.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 32 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.59 | 10.50 | 99% | 0.69 | 2.64 | 4.83 | 132.8 → 129.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 2.62 | 15.18 | 98% | 0.61 | 2.09 | 4.35 | 345.6 → 339.0 | 7 | 1.1 / 4 |

