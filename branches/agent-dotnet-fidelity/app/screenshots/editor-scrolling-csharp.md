# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 3.44 | 12.40 | 99% | 0.57 | 2.94 | 5.29 | 86.5 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.17 | 3.92 | 88.23 | 99% | 0.94 | 3.08 | 5.47 | 124.3 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.38 | 18.07 | 99% | 0.82 | 2.78 | 4.70 | 159.9 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.45 | 15.41 | 99% | 0.69 | 2.87 | 5.03 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 3.14 | 13.72 | 99% | 0.58 | 2.69 | 4.69 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.57 | 12.26 | 99% | 0.72 | 2.93 | 4.86 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.28 | 17.47 | 99% | 0.60 | 2.78 | 4.68 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 3.21 | 16.53 | 99% | 0.61 | 2.73 | 4.56 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 3.14 | 13.15 | 99% | 0.51 | 2.71 | 4.47 | 83.8 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.49 | 14.51 | 99% | 0.73 | 2.89 | 5.04 | 109.2 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 3.20 | 16.33 | 99% | 0.62 | 2.73 | 4.04 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.83 | 8.63 | 100% | 0.92 | 2.46 | 3.59 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable LineDrawing | 0.059 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.62 | 3.09 | 3.16 | 100% | 0.44 | 2.58 | 4.49 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 199 frames at 120 Hz; the text stood still on 0 of them, 232 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.17 | 17.25 | 99% | 0.57 | 2.72 | 4.62 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 3.68 | 19.31 | 99% | 0.72 | 3.09 | 4.68 | 318.5 → 310.4 | 7 | 1.1 / 4 |

