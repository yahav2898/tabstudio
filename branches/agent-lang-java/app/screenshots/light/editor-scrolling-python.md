# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.64 | 1.90 | 100% | 0.40 | 1.33 | 3.82 | 122.2 → 129.7 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.14 | 51.87 | 100% | 0.61 | 1.48 | 4.42 | 158.8 → 156.8 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.72 | 21.52 | 100% | 2.36 | 2.60 | 4.82 | 206.4 → 191.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 1.73 | 2.01 | 100% | 0.57 | 1.34 | 4.41 | 144.0 → 145.3 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 1.66 | 3.05 | 100% | 0.57 | 1.25 | 4.58 | 118.2 → 116.5 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 1.81 | 45.11 | 100% | 0.59 | 1.32 | 4.43 | 143.2 → 139.7 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.81 | 4.21 | 100% | 0.63 | 2.26 | 4.71 | 145.8 → 142.7 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.70 | 19.81 | 100% | 0.62 | 2.38 | 4.63 | 149.0 → 145.3 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.63 | 1.99 | 100% | 0.43 | 1.31 | 3.69 | 107.2 → 105.5 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 1.81 | 40.79 | 100% | 0.58 | 1.38 | 4.59 | 148.5 → 145.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 1.80 | 43.42 | 100% | 0.57 | 1.35 | 4.14 | 147.9 → 144.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.38 | 70.33 | 100% | 0.50 | 1.20 | 3.90 | 68.6 → 68.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.346 |
| ArrangingLayoutable CachedLineNumberMargin | 0.041 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 1.83 | 2.30 | 100% | 0.58 | 1.40 | 4.61 | 144.6 → 145.4 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.03 | 3.76 | 69.22 | 99% | 0.68 | 3.28 | 4.96 | 552.2 → 566.1 | 2 | 3.5 / 31 |

