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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 1.64 | 54.34 | 100% | 0.36 | 1.37 | 3.89 | 113.6 → 115.1 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 1.89 | 2.11 | 100% | 0.57 | 1.44 | 4.01 | 151.9 → 149.7 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 4.12 | 42.76 | 100% | 2.60 | 3.56 | 4.29 | 188.2 → 172.8 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 1.87 | 33.41 | 100% | 0.53 | 1.46 | 3.99 | 131.7 → 128.7 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 1.69 | 2.05 | 100% | 0.49 | 1.30 | 3.77 | 107.6 → 100.7 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 1.77 | 37.35 | 100% | 0.53 | 1.36 | 3.94 | 133.9 → 125.7 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 1.82 | 2.99 | 100% | 0.53 | 1.43 | 3.93 | 133.8 → 125.8 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.78 | 9.46 | 100% | 0.56 | 2.30 | 4.09 | 133.5 → 124.9 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 1.70 | 2.01 | 100% | 0.43 | 1.34 | 3.74 | 110.2 → 105.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 1.83 | 38.21 | 100% | 0.49 | 1.42 | 4.00 | 136.6 → 128.6 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.79 | 40.44 | 100% | 0.53 | 1.43 | 3.62 | 136.2 → 128.1 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.46 | 3.47 | 100% | 0.43 | 1.15 | 2.86 | 71.7 → 68.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable CachedLineNumberMargin | 0.029 |
| AttachingStyle - | 0.024 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| ArrangingLayoutable RepeatButton | 0.013 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 2.76 | 3.90 | 100% | 0.45 | 1.46 | 4.85 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 13 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 1.79 | 39.36 | 100% | 0.51 | 1.41 | 4.01 | 132.2 → 129.3 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 2.23 | 48.76 | 100% | 0.59 | 1.73 | 4.08 | 345.4 → 338.8 | 1 | 1.1 / 4 |

