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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.09 | 11.27 | 99% | 0.48 | 1.64 | 3.93 | 121.4 → 129.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 2.03 | 11.59 | 99% | 0.54 | 1.61 | 4.06 | 157.9 → 155.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 2.39 | 12.66 | 99% | 0.77 | 1.78 | 4.26 | 204.7 → 189.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.87 | 11.24 | 99% | 0.86 | 2.49 | 4.34 | 143.7 → 145.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.89 | 8.25 | 100% | 0.51 | 1.50 | 3.94 | 118.0 → 116.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.28 | 12.70 | 99% | 0.62 | 1.77 | 4.15 | 142.7 → 139.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.24 | 11.82 | 99% | 0.57 | 1.81 | 4.17 | 145.4 → 141.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.43 | 13.59 | 99% | 0.64 | 1.85 | 4.45 | 147.8 → 144.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.09 | 10.50 | 99% | 0.55 | 2.49 | 4.22 | 107.9 → 106.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 2.57 | 12.45 | 99% | 0.70 | 1.93 | 4.36 | 147.6 → 145.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.34 | 15.61 | 99% | 0.61 | 1.77 | 3.74 | 147.8 → 144.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.70 | 5.79 | 100% | 0.42 | 1.38 | 3.22 | 68.7 → 68.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.186 |
| ArrangingLayoutable CachedLineNumberMargin | 0.041 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.04 | 14.04 | 99% | 0.64 | 2.50 | 4.31 | 143.6 → 145.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.77 | 4.82 | 21.16 | 96% | 0.67 | 3.64 | 4.70 | 553.0 → 569.8 | 12 | 3.5 / 31 |

