# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 1.59 | 9.58 | 99% | 0.37 | 1.27 | 2.85 | 111.8 → 113.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 3.87 | 11.66 | 99% | 0.61 | 2.55 | 3.27 | 150.8 → 148.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.46 | 1.45 | 13.57 | 99% | 0.45 | 1.10 | 2.56 | 185.2 → 169.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 1.28 | 8.46 | 99% | 0.30 | 1.07 | 2.40 | 129.3 → 126.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 1.20 | 7.31 | 100% | 0.32 | 0.97 | 2.31 | 105.6 → 98.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.47 | 1.61 | 13.68 | 99% | 0.43 | 1.25 | 2.74 | 131.8 → 124.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.45 | 1.54 | 10.86 | 99% | 0.40 | 1.29 | 2.80 | 132.0 → 123.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.46 | 2.75 | 11.60 | 99% | 0.52 | 1.47 | 2.80 | 130.9 → 123.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 2.51 | 8.07 | 100% | 0.36 | 2.28 | 2.71 | 109.2 → 103.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 1.63 | 12.64 | 99% | 0.44 | 1.29 | 2.90 | 134.2 → 127.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 1.61 | 10.51 | 99% | 0.41 | 1.31 | 2.61 | 134.7 → 126.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.03 | 7.73 | 100% | 0.25 | 0.89 | 1.78 | 70.0 → 66.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.105 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.29 | 1.30 | 1.71 | 100% | 0.27 | 1.03 | 2.53 | 76.3 → 98.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 3.22 | 10.30 | 99% | 0.59 | 2.49 | 3.32 | 130.8 → 127.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.62 | 1.78 | 13.64 | 98% | 0.38 | 1.43 | 2.81 | 342.4 → 335.6 | 8 | 1.1 / 4 |

