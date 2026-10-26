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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 2.52 | 14.29 | 99% | 0.55 | 2.00 | 4.49 | 117.7 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 2.67 | 13.71 | 99% | 0.71 | 2.02 | 4.39 | 155.8 → 153.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.13 | 2.75 | 16.16 | 99% | 0.79 | 2.03 | 4.35 | 191.3 → 175.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 2.37 | 9.73 | 100% | 0.57 | 1.87 | 4.20 | 135.5 → 132.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.88 | 3.07 | 15.27 | 99% | 0.67 | 2.46 | 4.25 | 112.4 → 105.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 2.52 | 12.62 | 99% | 0.70 | 1.98 | 4.44 | 137.8 → 129.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.04 | 2.70 | 16.95 | 99% | 0.70 | 2.07 | 4.50 | 137.7 → 130.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 2.56 | 13.63 | 99% | 0.69 | 1.99 | 4.44 | 136.9 → 129.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.98 | 2.47 | 11.03 | 99% | 0.57 | 1.91 | 4.19 | 114.9 → 110.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.12 | 4.41 | 12.58 | 99% | 0.75 | 2.70 | 4.61 | 140.8 → 133.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.04 | 2.66 | 13.62 | 99% | 0.66 | 2.00 | 4.03 | 140.3 → 132.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 2.26 | 8.38 | 100% | 0.59 | 1.70 | 3.40 | 76.4 → 73.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.269 |
| AttachingStyle - | 0.147 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.96 | 2.46 | 3.12 | 100% | 0.60 | 1.90 | 4.64 | 82.8 → 104.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 152 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 3.56 | 14.07 | 99% | 0.74 | 2.38 | 4.62 | 136.1 → 133.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.40 | 3.11 | 18.39 | 98% | 0.75 | 2.36 | 4.44 | 348.6 → 341.7 | 8 | 1.1 / 4 |

