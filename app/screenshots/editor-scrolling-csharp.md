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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.08 | 3.78 | 14.26 | 100% | 0.65 | 3.17 | 5.58 | 84.9 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 4.29 | 17.86 | 100% | 1.10 | 3.29 | 5.70 | 122.1 → 122.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.55 | 4.37 | 18.68 | 99% | 2.20 | 3.35 | 6.11 | 158.2 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.15 | 3.93 | 19.77 | 99% | 0.85 | 3.15 | 5.53 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 3.69 | 14.93 | 99% | 0.83 | 2.95 | 5.39 | 80.5 → 73.7 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 1.19 | 4.09 | 15.52 | 99% | 0.96 | 3.15 | 5.54 | 105.1 → 96.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.15 | 3.83 | 20.62 | 99% | 0.89 | 3.13 | 5.78 | 104.9 → 96.7 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.19 | 3.86 | 17.41 | 99% | 0.87 | 3.04 | 5.51 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without coloring | 368 | 1.13 | 3.70 | 13.17 | 100% | 0.83 | 2.97 | 5.39 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.20 | 3.89 | 31.72 | 99% | 0.94 | 3.09 | 5.51 | 107.6 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.16 | 3.83 | 19.61 | 99% | 0.89 | 3.07 | 4.88 | 107.6 → 99.3 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 3.29 | 13.27 | 100% | 0.96 | 2.62 | 4.08 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.357 |
| AttachingStyle - | 0.083 |
| ArrangingLayoutable RepeatButton | 0.062 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable LineDrawing | 0.024 |
| FindingResource - | 0.017 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.98 | 3.61 | 44.28 | 98% | 0.63 | 2.92 | 5.50 | 49.3 → 73.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 179 frames at 120 Hz; the text stood still on 0 of them, 213 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 3.95 | 17.40 | 99% | 0.88 | 3.19 | 5.63 | 103.3 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.81 | 6.36 | 26.02 | 99% | 2.29 | 5.28 | 5.71 | 308.9 → 301.1 | 7 | 1.1 / 4 |

