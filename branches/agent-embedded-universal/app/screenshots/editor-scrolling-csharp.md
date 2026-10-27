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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.12 | 11.44 | 99% | 0.40 | 1.79 | 4.33 | 119.8 → 121.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.39 | 13.25 | 99% | 0.62 | 1.87 | 4.44 | 158.0 → 155.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 2.52 | 15.29 | 99% | 0.74 | 1.97 | 4.70 | 193.7 → 178.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.91 | 12.60 | 99% | 0.59 | 2.12 | 4.42 | 137.6 → 135.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.66 | 14.61 | 99% | 0.55 | 1.98 | 4.31 | 114.8 → 107.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.15 | 17.47 | 99% | 0.53 | 1.74 | 4.28 | 139.9 → 132.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 2.14 | 12.35 | 99% | 0.53 | 1.78 | 4.21 | 139.8 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 2.07 | 12.09 | 99% | 0.51 | 1.71 | 4.11 | 139.3 → 131.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.98 | 11.47 | 99% | 0.49 | 2.51 | 4.24 | 117.2 → 112.8 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 2.19 | 10.87 | 99% | 0.51 | 1.76 | 4.23 | 142.4 → 135.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.12 | 13.61 | 99% | 0.52 | 1.72 | 3.59 | 142.3 → 134.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.70 | 10.45 | 99% | 0.43 | 1.43 | 3.06 | 78.2 → 74.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.80 | 11.00 | 98% | 0.45 | 1.60 | 4.72 | 85.1 → 105.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 40 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 2.38 | 12.79 | 99% | 0.55 | 1.90 | 4.42 | 138.6 → 135.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 2.55 | 19.87 | 98% | 0.63 | 2.08 | 4.39 | 350.3 → 343.7 | 7 | 1.1 / 4 |

