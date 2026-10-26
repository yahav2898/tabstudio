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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.64 | 1.86 | 100% | 0.35 | 1.35 | 3.78 | 113.4 → 114.7 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 1.84 | 35.92 | 100% | 0.56 | 1.41 | 3.87 | 151.8 → 149.6 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 1.96 | 41.90 | 100% | 0.64 | 1.42 | 3.94 | 188.1 → 172.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.78 | 4.13 | 100% | 0.54 | 1.96 | 3.85 | 131.5 → 128.7 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.57 | 20.36 | 100% | 0.53 | 1.32 | 3.67 | 108.2 → 100.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 1.76 | 2.15 | 100% | 0.49 | 1.34 | 3.79 | 133.6 → 125.5 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 1.75 | 37.58 | 100% | 0.51 | 1.36 | 3.79 | 133.7 → 125.5 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 1.72 | 2.04 | 100% | 0.49 | 1.31 | 3.72 | 132.7 → 124.6 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.77 | 39.41 | 100% | 0.51 | 2.35 | 3.80 | 110.3 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 1.99 | 29.28 | 100% | 0.52 | 1.47 | 3.86 | 136.4 → 128.6 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.68 | 2.10 | 100% | 0.50 | 1.33 | 3.46 | 135.7 → 127.9 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.44 | 2.27 | 100% | 0.42 | 1.10 | 2.82 | 71.6 → 67.9 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable LineDrawing | 0.171 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 1.58 | 1.80 | 100% | 0.33 | 1.20 | 3.90 | 77.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 8 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 1.99 | 12.25 | 100% | 0.51 | 1.44 | 3.91 | 132.3 → 128.8 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 2.10 | 45.34 | 99% | 0.57 | 1.64 | 3.89 | 345.4 → 338.9 | 2 | 1.1 / 4 |

