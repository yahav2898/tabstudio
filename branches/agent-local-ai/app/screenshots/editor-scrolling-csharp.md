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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.22 | 11.62 | 99% | 0.44 | 1.83 | 4.59 | 114.0 → 115.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.26 | 12.59 | 99% | 0.58 | 1.79 | 4.38 | 152.3 → 150.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.42 | 15.17 | 99% | 0.72 | 1.79 | 4.36 | 188.4 → 172.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 3.17 | 13.11 | 99% | 0.77 | 2.40 | 4.88 | 132.0 → 129.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.21 | 8.10 | 100% | 0.58 | 1.74 | 4.45 | 108.4 → 101.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 2.51 | 16.98 | 99% | 0.64 | 1.94 | 4.78 | 134.4 → 126.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.08 | 13.18 | 99% | 0.52 | 1.67 | 4.27 | 134.5 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.63 | 72.49 | 99% | 0.72 | 2.63 | 4.88 | 133.5 → 125.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 1.94 | 10.21 | 99% | 0.45 | 1.60 | 4.04 | 110.9 → 105.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.01 | 11.90 | 99% | 0.52 | 1.66 | 4.20 | 136.6 → 129.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.32 | 13.18 | 99% | 0.57 | 1.79 | 4.19 | 136.7 → 128.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.47 | 69.37 | 99% | 0.57 | 1.73 | 3.76 | 72.1 → 69.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.216 |
| AttachingStyle - | 0.028 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 1.90 | 2.02 | 100% | 0.41 | 1.64 | 4.65 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 35 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.20 | 11.83 | 99% | 0.55 | 1.77 | 4.52 | 132.4 → 129.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 5.10 | 21.36 | 98% | 2.44 | 4.44 | 5.18 | 347.1 → 340.0 | 9 | 1.1 / 4 |

