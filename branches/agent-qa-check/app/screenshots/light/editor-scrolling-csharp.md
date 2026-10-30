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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 3.03 | 21.64 | 100% | 0.54 | 2.57 | 5.69 | 86.1 → 87.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.09 | 3.40 | 20.70 | 100% | 0.87 | 2.62 | 5.79 | 123.5 → 121.2 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 3.52 | 24.52 | 99% | 1.00 | 2.67 | 6.17 | 158.6 → 143.0 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 3.33 | 22.69 | 100% | 0.73 | 2.67 | 5.98 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.84 | 3.05 | 20.09 | 99% | 0.71 | 2.43 | 5.58 | 80.7 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 3.18 | 4.15 | 100% | 0.71 | 2.57 | 6.01 | 105.8 → 97.6 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.23 | 22.20 | 99% | 0.70 | 2.60 | 5.91 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.00 | 3.50 | 12.94 | 100% | 1.47 | 2.62 | 5.78 | 105.1 → 96.8 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 3.07 | 19.14 | 100% | 0.60 | 2.49 | 5.58 | 83.5 → 78.1 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 3.16 | 22.32 | 99% | 0.74 | 2.58 | 5.78 | 108.4 → 100.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.96 | 3.50 | 21.03 | 99% | 2.55 | 2.67 | 5.21 | 108.5 → 100.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.63 | 2.78 | 3.98 | 100% | 0.66 | 2.27 | 4.45 | 46.1 → 42.2 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.350 |
| AttachingStyle - | 0.088 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 2.89 | 3.40 | 100% | 0.51 | 2.40 | 5.89 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 179 frames at 120 Hz; the text stood still on 0 of them, 213 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.03 | 4.42 | 22.00 | 100% | 1.64 | 2.66 | 5.87 | 104.2 → 101.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.47 | 3.68 | 24.75 | 99% | 0.80 | 2.91 | 5.86 | 316.7 → 308.6 | 5 | 1.1 / 4 |

