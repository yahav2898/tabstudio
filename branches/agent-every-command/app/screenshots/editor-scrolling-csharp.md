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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.93 | 3.08 | 21.91 | 100% | 0.56 | 2.55 | 5.88 | 84.9 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.18 | 3.32 | 25.72 | 100% | 0.83 | 2.58 | 5.97 | 122.1 → 122.6 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.29 | 3.71 | 25.34 | 99% | 1.53 | 2.70 | 10.26 | 158.2 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 3.60 | 92.46 | 99% | 1.62 | 2.68 | 5.98 | 102.3 → 99.5 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.84 | 3.04 | 20.29 | 100% | 0.73 | 2.42 | 5.51 | 80.5 → 73.7 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 1.00 | 3.25 | 22.68 | 100% | 0.73 | 2.54 | 5.64 | 105.1 → 96.7 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.03 | 3.24 | 23.51 | 99% | 0.79 | 2.60 | 5.77 | 105.0 → 96.7 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.12 | 4.87 | 24.36 | 99% | 1.74 | 2.74 | 6.52 | 104.3 → 96.0 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.94 | 3.07 | 21.21 | 100% | 0.65 | 2.45 | 5.73 | 82.7 → 77.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 1.04 | 3.22 | 22.39 | 100% | 0.80 | 2.57 | 5.94 | 107.5 → 99.4 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.02 | 3.25 | 23.82 | 99% | 0.75 | 2.58 | 5.32 | 107.6 → 99.3 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 2.96 | 15.91 | 100% | 1.36 | 2.31 | 4.52 | 46.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.337 |
| AttachingStyle - | 0.034 |
| ArrangingLayoutable TextView | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.89 | 3.21 | 3.38 | 100% | 0.56 | 2.51 | 6.08 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 169 frames at 120 Hz; the text stood still on 0 of them, 204 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 3.33 | 25.51 | 100% | 0.76 | 2.65 | 5.92 | 103.4 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.67 | 5.63 | 29.13 | 99% | 1.62 | 4.97 | 6.17 | 309.0 → 301.1 | 5 | 1.1 / 4 |

