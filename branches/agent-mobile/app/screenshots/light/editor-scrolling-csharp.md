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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.33 | 1.23 | 10.23 | 100% | 0.22 | 1.07 | 2.37 | 111.5 → 112.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.41 | 1.39 | 8.97 | 100% | 0.37 | 1.09 | 2.50 | 149.7 → 147.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.46 | 1.45 | 10.70 | 99% | 0.43 | 1.13 | 2.50 | 185.4 → 169.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.37 | 1.29 | 9.20 | 100% | 0.31 | 1.07 | 2.36 | 129.2 → 126.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.30 | 1.22 | 7.21 | 100% | 0.29 | 1.00 | 2.25 | 105.8 → 98.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.46 | 2.74 | 13.79 | 99% | 0.40 | 2.35 | 2.78 | 131.9 → 124.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 1.50 | 8.11 | 100% | 0.34 | 1.16 | 2.63 | 131.7 → 123.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.30 | 8.63 | 99% | 0.34 | 1.07 | 2.64 | 130.7 → 123.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.32 | 1.17 | 7.35 | 100% | 0.25 | 1.01 | 2.21 | 108.5 → 103.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.34 | 1.22 | 8.82 | 100% | 0.30 | 1.03 | 2.32 | 134.0 → 126.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 1.21 | 8.02 | 100% | 0.30 | 1.04 | 2.17 | 134.1 → 126.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.18 | 7.12 | 100% | 0.28 | 0.91 | 1.80 | 70.0 → 66.6 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.243 |
| ArrangingLayoutable CachedLineNumberMargin | 0.049 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| ArrangingLayoutable Border | 0.012 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 2.05 | 4.37 | 100% | 0.35 | 1.70 | 3.08 | 77.3 → 98.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 1.74 | 8.74 | 99% | 0.32 | 1.55 | 2.50 | 130.2 → 127.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 1.98 | 11.43 | 98% | 0.40 | 1.74 | 3.02 | 342.7 → 336.1 | 8 | 1.1 / 4 |

