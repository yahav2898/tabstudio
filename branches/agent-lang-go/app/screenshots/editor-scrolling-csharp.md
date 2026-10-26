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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.06 | 8.87 | 99% | 0.39 | 1.70 | 4.36 | 113.9 → 115.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.14 | 15.17 | 99% | 0.56 | 1.73 | 4.24 | 152.4 → 150.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 2.37 | 13.88 | 99% | 0.66 | 1.73 | 4.40 | 188.4 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.12 | 11.33 | 99% | 0.61 | 2.44 | 4.83 | 132.1 → 129.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.78 | 9.76 | 100% | 0.47 | 1.46 | 3.87 | 108.1 → 101.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.01 | 13.93 | 99% | 0.50 | 1.62 | 4.07 | 134.1 → 126.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 1.99 | 12.16 | 99% | 0.52 | 1.63 | 4.30 | 134.1 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.13 | 14.33 | 99% | 0.78 | 2.43 | 4.90 | 133.7 → 125.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 1.95 | 8.30 | 99% | 0.42 | 1.65 | 4.04 | 110.9 → 105.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.16 | 11.37 | 99% | 0.46 | 1.73 | 4.39 | 136.8 → 128.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 1.98 | 12.36 | 99% | 0.51 | 1.63 | 3.74 | 136.8 → 128.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.61 | 9.41 | 100% | 0.63 | 1.72 | 3.83 | 72.1 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable TextView | 0.048 |
| ArrangingLayoutable Track | 0.043 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 1.95 | 2.25 | 100% | 0.40 | 1.66 | 4.78 | 78.5 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 208 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.06 | 13.44 | 99% | 0.51 | 1.71 | 4.29 | 132.5 → 129.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.25 | 4.84 | 72.86 | 98% | 0.80 | 4.36 | 4.99 | 347.2 → 338.9 | 7 | 1.1 / 4 |

