# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 3.59 | 13.88 | 100% | 0.52 | 3.33 | 4.20 | 119.0 → 120.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 1.48 | 11.31 | 99% | 0.39 | 1.18 | 2.87 | 156.0 → 153.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.47 | 1.49 | 12.19 | 99% | 0.43 | 1.19 | 2.59 | 191.5 → 175.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.38 | 1.37 | 8.56 | 99% | 0.30 | 1.18 | 2.56 | 135.6 → 132.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.30 | 1.24 | 7.99 | 100% | 0.29 | 1.05 | 2.32 | 112.2 → 105.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.35 | 1.32 | 8.60 | 99% | 0.35 | 1.13 | 2.50 | 137.7 → 129.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.40 | 8.83 | 99% | 0.31 | 1.16 | 2.58 | 137.6 → 129.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.50 | 2.85 | 13.25 | 99% | 0.61 | 2.31 | 2.98 | 137.7 → 129.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.37 | 1.60 | 7.79 | 100% | 0.30 | 1.32 | 2.71 | 115.6 → 109.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.37 | 1.39 | 8.79 | 100% | 0.31 | 1.17 | 2.48 | 140.2 → 132.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.36 | 1.32 | 8.70 | 99% | 0.31 | 1.15 | 2.12 | 140.0 → 132.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.16 | 7.14 | 100% | 0.25 | 1.02 | 1.73 | 76.4 → 73.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.147 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.29 | 1.75 | 1.96 | 100% | 0.21 | 1.58 | 2.56 | 82.6 → 105.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 8 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.47 | 2.43 | 11.57 | 100% | 0.36 | 1.81 | 3.04 | 137.1 → 133.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.60 | 2.17 | 13.04 | 98% | 0.36 | 1.88 | 2.63 | 348.9 → 342.3 | 8 | 1.1 / 4 |

