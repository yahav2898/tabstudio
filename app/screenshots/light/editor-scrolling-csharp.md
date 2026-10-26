# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 1.74 | 9.82 | 99% | 0.32 | 1.49 | 3.41 | 118.1 → 119.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.64 | 2.00 | 12.64 | 99% | 0.49 | 1.58 | 3.65 | 156.2 → 154.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 2.03 | 12.59 | 99% | 0.57 | 1.52 | 3.52 | 191.5 → 175.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 1.81 | 12.50 | 99% | 0.42 | 1.46 | 3.43 | 135.6 → 133.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 3.04 | 85.30 | 99% | 0.56 | 2.30 | 3.57 | 113.1 → 106.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 1.86 | 11.48 | 99% | 0.45 | 1.51 | 3.49 | 138.5 → 130.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.50 | 1.73 | 13.65 | 99% | 0.41 | 1.42 | 3.33 | 138.3 → 130.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 1.67 | 10.26 | 99% | 0.40 | 1.39 | 3.17 | 137.1 → 129.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.64 | 9.34 | 99% | 0.34 | 1.37 | 3.17 | 115.5 → 110.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 1.72 | 10.61 | 99% | 0.42 | 1.41 | 3.29 | 140.5 → 132.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.92 | 13.78 | 99% | 0.50 | 2.50 | 3.30 | 140.9 → 133.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.81 | 8.88 | 100% | 0.38 | 1.45 | 2.71 | 77.8 → 73.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 1.99 | 9.90 | 98% | 0.37 | 1.52 | 3.74 | 83.8 → 105.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 7 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 1.79 | 11.89 | 99% | 0.45 | 1.53 | 3.50 | 136.5 → 133.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.84 | 2.14 | 15.66 | 98% | 0.48 | 1.72 | 3.51 | 348.3 → 341.2 | 7 | 1.1 / 4 |

