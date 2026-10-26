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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.70 | 38.98 | 100% | 0.35 | 1.41 | 3.80 | 112.6 → 114.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 1.91 | 2.17 | 100% | 0.59 | 1.45 | 4.01 | 150.8 → 148.6 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 2.38 | 44.07 | 100% | 0.83 | 1.52 | 4.12 | 186.4 → 170.7 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.10 | 20.95 | 100% | 0.68 | 2.41 | 3.98 | 130.9 → 127.7 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.63 | 1.88 | 100% | 0.50 | 1.27 | 3.73 | 107.1 → 100.2 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 1.82 | 37.94 | 100% | 0.53 | 1.38 | 3.90 | 132.7 → 125.0 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 1.80 | 2.19 | 100% | 0.52 | 1.40 | 3.90 | 133.0 → 124.8 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.77 | 41.37 | 100% | 0.58 | 2.03 | 4.01 | 132.2 → 124.2 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.63 | 4.36 | 100% | 0.49 | 1.40 | 3.72 | 110.5 → 104.6 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 1.76 | 26.87 | 100% | 0.51 | 1.37 | 3.90 | 135.6 → 127.5 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.73 | 38.89 | 100% | 0.50 | 1.35 | 3.45 | 134.8 → 126.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.42 | 2.01 | 100% | 0.44 | 1.12 | 2.84 | 71.5 → 68.0 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.187 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 2.67 | 3.58 | 100% | 0.48 | 1.38 | 4.12 | 78.2 → 98.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 135 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 1.79 | 37.01 | 100% | 0.51 | 1.40 | 3.95 | 131.2 → 128.3 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 2.10 | 48.71 | 100% | 0.58 | 1.66 | 3.90 | 343.3 → 336.7 | 1 | 1.1 / 4 |

