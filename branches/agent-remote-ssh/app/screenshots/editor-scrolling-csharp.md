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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 1.83 | 10.52 | 99% | 0.38 | 1.44 | 3.13 | 114.2 → 115.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 1.92 | 12.47 | 99% | 0.56 | 1.49 | 3.13 | 152.2 → 150.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 1.87 | 11.99 | 99% | 0.55 | 1.43 | 2.93 | 187.7 → 172.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.92 | 13.82 | 99% | 0.52 | 2.43 | 3.06 | 132.0 → 129.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.48 | 6.35 | 100% | 0.46 | 1.45 | 2.97 | 109.0 → 101.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 1.85 | 14.93 | 99% | 0.55 | 1.42 | 3.23 | 134.4 → 126.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.00 | 9.92 | 100% | 0.53 | 1.52 | 3.50 | 134.4 → 126.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 1.68 | 12.42 | 99% | 0.47 | 1.32 | 3.19 | 133.5 → 125.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.60 | 9.39 | 100% | 0.39 | 1.28 | 2.73 | 110.8 → 106.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.02 | 10.62 | 99% | 0.48 | 1.49 | 3.03 | 136.6 → 129.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 3.28 | 14.09 | 99% | 1.38 | 2.38 | 2.89 | 136.7 → 129.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.41 | 6.50 | 100% | 0.40 | 1.12 | 2.14 | 72.9 → 69.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| ArrangingLayoutable LineDrawing | 0.043 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.38 | 1.30 | 1.50 | 100% | 0.29 | 1.02 | 2.99 | 78.4 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 3 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 1.93 | 11.15 | 99% | 0.52 | 1.52 | 3.23 | 133.0 → 129.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.97 | 3.63 | 19.41 | 98% | 0.58 | 2.75 | 3.38 | 344.6 → 339.0 | 9 | 1.1 / 4 |

