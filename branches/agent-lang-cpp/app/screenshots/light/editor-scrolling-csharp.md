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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 1.30 | 11.43 | 100% | 0.23 | 1.08 | 2.49 | 111.6 → 112.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 1.41 | 9.36 | 99% | 0.37 | 1.16 | 2.61 | 149.8 → 147.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.59 | 1.73 | 11.43 | 99% | 0.48 | 1.31 | 2.90 | 185.7 → 169.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.54 | 9.76 | 99% | 0.37 | 1.27 | 2.69 | 129.5 → 126.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.58 | 8.67 | 100% | 0.46 | 1.31 | 2.75 | 106.1 → 99.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 3.08 | 11.30 | 99% | 0.55 | 2.44 | 3.64 | 132.9 → 124.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 1.60 | 10.09 | 99% | 0.44 | 1.25 | 2.86 | 132.1 → 124.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.46 | 10.16 | 99% | 0.66 | 1.89 | 6.07 | 131.4 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.58 | 10.31 | 100% | 0.36 | 1.25 | 2.72 | 109.5 → 104.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 1.57 | 9.81 | 99% | 0.40 | 1.28 | 2.87 | 134.7 → 126.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.72 | 13.22 | 99% | 0.55 | 2.23 | 2.88 | 134.8 → 126.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.74 | 4.12 | 100% | 0.43 | 1.29 | 2.48 | 70.7 → 66.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.124 |
| AttachingStyle - | 0.046 |
| ArrangingLayoutable TextView | 0.008 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.45 | 1.36 | 1.55 | 100% | 0.31 | 1.15 | 2.81 | 76.3 → 98.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 1.71 | 10.37 | 99% | 0.39 | 1.35 | 2.85 | 130.4 → 127.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.75 | 1.88 | 15.43 | 98% | 0.44 | 1.50 | 2.82 | 342.7 → 335.9 | 9 | 1.1 / 4 |

