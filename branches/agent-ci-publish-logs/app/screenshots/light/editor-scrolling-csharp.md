# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.38 | 2.10 | 14.32 | 99% | 0.30 | 1.87 | 2.61 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.19 | 14.19 | 99% | 0.42 | 1.89 | 2.56 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.60 | 3.01 | 14.12 | 99% | 1.39 | 2.10 | 2.97 | 156.7 → 141.7 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.86 | 100.09 | 99% | 0.60 | 2.35 | 3.15 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 2.01 | 13.69 | 99% | 0.39 | 1.76 | 2.39 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.38 | 2.04 | 15.06 | 100% | 0.35 | 1.79 | 2.51 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.40 | 2.13 | 15.60 | 99% | 0.42 | 1.83 | 2.67 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.40 | 2.12 | 13.28 | 99% | 0.38 | 1.81 | 2.52 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.37 | 2.06 | 8.41 | 100% | 0.32 | 1.83 | 2.42 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.43 | 2.23 | 11.69 | 100% | 0.42 | 1.93 | 2.73 | 106.7 → 98.8 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.43 | 2.50 | 14.99 | 99% | 0.48 | 2.07 | 2.50 | 106.8 → 98.6 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.38 | 85.06 | 99% | 1.26 | 1.87 | 2.06 | 46.1 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| AttachingStyle - | 0.026 |
| FindingResource - | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.39 | 2.03 | 2.46 | 100% | 0.30 | 1.76 | 2.59 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 11 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.41 | 2.19 | 20.31 | 99% | 0.39 | 1.91 | 2.62 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.67 | 2.56 | 16.99 | 98% | 0.45 | 2.17 | 2.82 | 307.6 → 299.8 | 6 | 1.1 / 4 |

