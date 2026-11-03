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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.01 | 3.73 | 18.54 | 99% | 0.61 | 3.10 | 5.40 | 84.9 → 85.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 4.04 | 18.11 | 99% | 0.90 | 3.17 | 5.54 | 122.2 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.54 | 6.14 | 39.33 | 98% | 2.95 | 3.44 | 6.31 | 158.2 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 3.66 | 14.25 | 100% | 0.79 | 3.01 | 5.17 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 1.03 | 3.66 | 20.61 | 100% | 0.85 | 2.94 | 5.46 | 80.6 → 73.7 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 1.13 | 3.86 | 20.18 | 99% | 0.85 | 3.08 | 5.80 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.18 | 3.92 | 19.14 | 99% | 0.91 | 3.14 | 5.57 | 105.0 → 96.7 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.18 | 3.94 | 19.68 | 99% | 0.91 | 3.16 | 5.54 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 1.08 | 3.78 | 13.82 | 99% | 0.71 | 3.07 | 5.31 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.16 | 3.87 | 14.01 | 99% | 0.79 | 3.11 | 5.94 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.11 | 3.90 | 17.72 | 99% | 0.79 | 3.12 | 4.79 | 107.6 → 99.3 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.76 | 3.53 | 13.72 | 100% | 1.64 | 2.82 | 4.36 | 46.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.958 |
| AttachingStyle - | 0.093 |
| ArrangingLayoutable NumberDrawing | 0.029 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable LineDrawing | 0.024 |
| FindingResource - | 0.018 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.07 | 3.86 | 45.24 | 98% | 0.66 | 3.18 | 5.74 | 49.5 → 73.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 180 frames at 120 Hz; the text stood still on 0 of them, 214 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.22 | 4.07 | 16.00 | 99% | 0.88 | 3.26 | 5.75 | 103.4 → 100.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.84 | 6.33 | 26.28 | 98% | 1.16 | 3.91 | 5.92 | 308.9 → 301.2 | 8 | 1.1 / 4 |

