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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.42 | 1.43 | 9.66 | 99% | 0.28 | 1.21 | 2.52 | 111.3 → 112.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.56 | 1.74 | 10.60 | 99% | 0.49 | 1.37 | 3.00 | 149.6 → 147.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.10 | 13.77 | 99% | 0.61 | 1.54 | 3.03 | 185.9 → 170.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 1.71 | 10.02 | 99% | 0.43 | 1.37 | 2.80 | 129.5 → 127.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.69 | 10.23 | 99% | 0.57 | 2.24 | 3.06 | 106.9 → 100.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 1.74 | 11.42 | 99% | 0.41 | 1.37 | 2.77 | 131.7 → 123.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.45 | 1.57 | 13.24 | 99% | 0.38 | 1.29 | 2.64 | 131.5 → 123.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.38 | 1.42 | 9.22 | 99% | 0.34 | 1.17 | 2.36 | 130.6 → 122.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 1.30 | 8.06 | 100% | 0.28 | 1.14 | 2.31 | 108.6 → 103.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.39 | 1.38 | 9.74 | 99% | 0.35 | 1.16 | 2.45 | 133.9 → 126.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 1.33 | 9.71 | 99% | 0.31 | 1.14 | 2.04 | 134.0 → 125.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.50 | 8.65 | 100% | 0.36 | 1.15 | 1.92 | 70.4 → 67.2 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.267 |
| AttachingStyle - | 0.096 |
| ArrangingLayoutable TextView | 0.063 |
| ArrangingLayoutable LineDrawing | 0.053 |
| ArrangingLayoutable CachedLineNumberMargin | 0.039 |
| ArrangingLayoutable Track | 0.018 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Grid | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 1.84 | 4.03 | 100% | 0.39 | 1.43 | 3.25 | 77.3 → 98.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.43 | 1.48 | 10.51 | 99% | 0.34 | 1.23 | 2.57 | 129.9 → 126.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.65 | 1.83 | 13.63 | 98% | 0.43 | 1.40 | 2.61 | 342.3 → 335.7 | 8 | 1.1 / 4 |

