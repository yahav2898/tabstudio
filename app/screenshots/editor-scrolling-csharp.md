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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.08 | 10.81 | 99% | 0.40 | 1.73 | 3.86 | 119.8 → 121.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.24 | 13.65 | 99% | 0.59 | 1.76 | 3.96 | 157.7 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 2.52 | 15.26 | 99% | 0.68 | 1.90 | 4.11 | 193.5 → 177.9 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 3.71 | 13.22 | 99% | 0.67 | 2.53 | 4.24 | 137.8 → 135.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 1.98 | 9.49 | 99% | 0.48 | 1.56 | 3.63 | 114.3 → 107.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 2.08 | 15.00 | 99% | 0.53 | 1.66 | 3.84 | 139.9 → 131.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.21 | 13.58 | 99% | 0.53 | 1.73 | 3.89 | 139.9 → 131.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.13 | 12.34 | 99% | 0.53 | 1.72 | 4.04 | 139.5 → 131.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.82 | 9.60 | 99% | 0.48 | 2.31 | 4.13 | 117.1 → 111.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 2.70 | 81.13 | 99% | 0.52 | 2.37 | 4.08 | 142.6 → 134.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.15 | 11.16 | 99% | 0.49 | 1.74 | 3.52 | 142.2 → 134.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.81 | 7.66 | 100% | 0.42 | 1.46 | 2.92 | 77.8 → 74.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| FindingResource - | 0.046 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.72 | 2.00 | 2.08 | 100% | 0.43 | 1.61 | 4.10 | 85.1 → 105.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 31 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.81 | 11.79 | 99% | 0.56 | 2.54 | 3.98 | 138.1 → 135.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.94 | 2.34 | 25.54 | 98% | 0.55 | 1.85 | 3.86 | 350.5 → 343.7 | 8 | 1.1 / 4 |

