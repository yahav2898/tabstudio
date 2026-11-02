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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 3.31 | 19.78 | 99% | 0.49 | 2.89 | 5.09 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 3.27 | 12.93 | 100% | 0.69 | 2.70 | 4.63 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.30 | 16.04 | 99% | 0.86 | 2.67 | 4.46 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.33 | 23.07 | 99% | 0.66 | 2.79 | 4.63 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 3.20 | 37.52 | 99% | 0.63 | 2.69 | 4.75 | 80.6 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.60 | 3.07 | 12.69 | 99% | 0.59 | 2.64 | 4.25 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 3.08 | 19.12 | 99% | 0.58 | 2.63 | 4.27 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.61 | 3.04 | 15.65 | 99% | 0.60 | 2.59 | 4.18 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 3.07 | 11.94 | 99% | 0.52 | 2.65 | 4.38 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.84 | 3.60 | 19.40 | 99% | 1.04 | 2.87 | 5.30 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 2.97 | 15.59 | 99% | 0.57 | 2.58 | 3.69 | 107.6 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.63 | 8.09 | 100% | 0.67 | 2.30 | 3.07 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.197 |
| AttachingStyle - | 0.062 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 3.03 | 3.19 | 100% | 0.42 | 2.55 | 4.56 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 202 frames at 120 Hz; the text stood still on 0 of them, 235 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 3.06 | 13.38 | 99% | 0.59 | 2.61 | 4.32 | 103.3 → 99.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.07 | 3.54 | 19.66 | 98% | 0.69 | 2.92 | 4.36 | 308.9 → 301.1 | 7 | 1.1 / 4 |

