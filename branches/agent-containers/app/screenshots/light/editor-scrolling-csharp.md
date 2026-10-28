# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.87 | 11.15 | 99% | 0.44 | 2.50 | 3.42 | 122.8 → 123.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.35 | 13.78 | 99% | 0.64 | 2.73 | 4.03 | 160.5 → 157.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.13 | 3.38 | 16.05 | 99% | 0.75 | 2.72 | 3.82 | 195.4 → 179.5 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 5.27 | 14.67 | 99% | 2.18 | 4.61 | 4.20 | 140.3 → 137.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 2.33 | 10.14 | 99% | 0.38 | 2.00 | 2.72 | 115.2 → 108.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.47 | 2.43 | 14.41 | 99% | 0.38 | 2.14 | 2.85 | 140.9 → 133.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 2.72 | 12.44 | 99% | 0.45 | 2.28 | 3.15 | 141.2 → 133.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.61 | 11.41 | 99% | 0.42 | 2.25 | 3.09 | 140.1 → 133.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.45 | 10.61 | 99% | 0.41 | 2.19 | 2.85 | 118.4 → 113.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 2.81 | 14.14 | 99% | 0.47 | 2.40 | 3.16 | 143.2 → 137.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 4.29 | 12.85 | 99% | 0.49 | 4.06 | 2.89 | 144.4 → 135.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.99 | 8.61 | 100% | 0.28 | 1.80 | 1.78 | 79.5 → 76.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.158 |
| ArrangingLayoutable TextView | 0.007 |
| AttachingStyle - | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 3.23 | 9.91 | 98% | 0.50 | 2.27 | 4.58 | 86.4 → 109.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 3 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.48 | 2.43 | 9.21 | 99% | 0.39 | 2.12 | 2.96 | 139.1 → 136.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 3.56 | 17.52 | 98% | 0.58 | 2.82 | 3.54 | 351.4 → 345.4 | 9 | 1.1 / 4 |

