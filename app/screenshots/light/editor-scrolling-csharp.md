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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.22 | 28.69 | 99% | 0.44 | 2.76 | 4.56 | 84.4 → 85.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 3.52 | 26.84 | 99% | 1.29 | 2.83 | 4.98 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 3.38 | 18.23 | 99% | 0.95 | 2.71 | 4.64 | 156.7 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.72 | 18.06 | 99% | 0.80 | 2.97 | 5.25 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 3.49 | 15.05 | 99% | 0.83 | 2.77 | 5.18 | 79.8 → 73.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 1.03 | 3.83 | 16.77 | 99% | 0.89 | 2.97 | 5.26 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.90 | 3.52 | 17.92 | 99% | 0.72 | 2.92 | 4.97 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.47 | 16.55 | 99% | 0.65 | 2.86 | 4.82 | 103.6 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.65 | 3.06 | 14.91 | 99% | 0.51 | 2.67 | 4.34 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.99 | 3.70 | 23.70 | 99% | 0.79 | 3.05 | 5.13 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.92 | 4.04 | 15.72 | 99% | 0.72 | 3.04 | 4.55 | 106.9 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 3.07 | 12.90 | 100% | 0.61 | 2.58 | 3.51 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.270 |
| AttachingStyle - | 0.070 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.017 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 3.04 | 3.55 | 100% | 0.49 | 2.67 | 4.91 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 187 frames at 120 Hz; the text stood still on 0 of them, 220 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.59 | 22.68 | 99% | 0.71 | 2.96 | 5.18 | 102.7 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.35 | 3.96 | 21.89 | 98% | 0.79 | 3.25 | 4.93 | 307.6 → 299.9 | 7 | 1.1 / 4 |

