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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 3.44 | 19.31 | 100% | 0.56 | 2.90 | 5.16 | 84.9 → 85.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.17 | 3.74 | 19.59 | 100% | 0.83 | 3.02 | 5.29 | 122.2 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 5.68 | 31.20 | 99% | 2.91 | 3.18 | 6.93 | 158.2 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.57 | 19.28 | 99% | 0.72 | 2.91 | 5.08 | 102.3 → 99.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 3.37 | 13.21 | 100% | 0.75 | 2.78 | 5.08 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.89 | 3.46 | 20.66 | 99% | 0.72 | 2.88 | 5.01 | 105.1 → 96.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.97 | 3.66 | 19.49 | 99% | 0.79 | 3.00 | 5.20 | 104.9 → 96.7 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.97 | 3.58 | 111.82 | 99% | 1.05 | 2.96 | 5.27 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.86 | 3.42 | 15.87 | 99% | 0.63 | 2.86 | 4.84 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.90 | 3.50 | 17.60 | 99% | 0.71 | 2.90 | 5.03 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.91 | 3.62 | 18.51 | 99% | 0.78 | 2.90 | 4.53 | 107.6 → 99.3 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.16 | 15.26 | 100% | 1.25 | 2.59 | 3.93 | 46.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.904 |
| AttachingStyle - | 0.104 |
| ArrangingLayoutable LineDrawing | 0.033 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.016 |
| ArrangingLayoutable Track | 0.010 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 3.49 | 40.86 | 98% | 0.51 | 2.99 | 5.25 | 49.3 → 73.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 186 frames at 120 Hz; the text stood still on 0 of them, 219 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 3.63 | 15.95 | 99% | 0.71 | 2.99 | 5.11 | 103.3 → 100.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.98 | 6.29 | 24.15 | 98% | 2.68 | 5.44 | 5.78 | 308.9 → 301.2 | 8 | 1.1 / 4 |

