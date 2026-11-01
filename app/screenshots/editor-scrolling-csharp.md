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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 2.82 | 3.91 | 100% | 0.36 | 2.56 | 4.14 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 3.30 | 16.80 | 99% | 0.59 | 2.74 | 4.41 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 3.29 | 19.82 | 99% | 0.88 | 2.69 | 4.41 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 4.92 | 37.11 | 99% | 1.57 | 2.83 | 4.55 | 102.3 → 99.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.81 | 18.27 | 100% | 0.59 | 2.40 | 4.17 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.64 | 3.12 | 18.01 | 99% | 0.60 | 2.60 | 4.36 | 105.1 → 96.6 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.63 | 3.02 | 16.70 | 99% | 0.61 | 2.63 | 4.42 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 3.01 | 15.14 | 99% | 0.56 | 2.61 | 4.17 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 3.36 | 13.26 | 99% | 0.53 | 2.76 | 4.39 | 82.6 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.41 | 17.93 | 99% | 0.63 | 2.87 | 4.59 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.68 | 3.13 | 19.27 | 99% | 0.55 | 2.70 | 3.87 | 107.6 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.75 | 10.18 | 100% | 0.62 | 2.36 | 3.17 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 3.18 | 4.89 | 100% | 0.45 | 2.74 | 4.44 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 223 frames at 120 Hz; the text stood still on 0 of them, 257 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 3.03 | 21.88 | 99% | 0.53 | 2.62 | 4.21 | 103.2 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 3.38 | 20.50 | 98% | 0.64 | 2.87 | 4.16 | 308.9 → 301.1 | 7 | 1.1 / 4 |

