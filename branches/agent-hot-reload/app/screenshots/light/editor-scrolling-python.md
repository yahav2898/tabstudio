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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.10 | 8.86 | 99% | 0.43 | 1.79 | 4.71 | 122.0 → 129.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.71 | 14.48 | 99% | 0.68 | 1.96 | 4.97 | 157.9 → 156.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 4.48 | 18.24 | 99% | 2.55 | 2.65 | 5.23 | 205.2 → 189.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.56 | 13.05 | 99% | 0.58 | 2.21 | 4.81 | 143.5 → 144.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.20 | 12.55 | 99% | 0.58 | 1.76 | 4.73 | 118.3 → 116.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 3.39 | 17.98 | 99% | 0.72 | 2.62 | 4.89 | 142.9 → 139.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.23 | 14.83 | 99% | 0.72 | 2.58 | 5.14 | 145.6 → 141.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.78 | 13.72 | 99% | 0.61 | 2.31 | 4.82 | 148.2 → 144.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.70 | 10.19 | 100% | 0.49 | 2.39 | 4.82 | 107.6 → 106.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 4.01 | 18.13 | 99% | 0.89 | 2.89 | 5.01 | 148.0 → 145.7 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.39 | 10.71 | 100% | 0.61 | 1.91 | 4.18 | 147.6 → 144.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.28 | 11.24 | 100% | 0.42 | 2.09 | 3.94 | 68.9 → 68.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.197 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.04 | 11.75 | 99% | 0.61 | 2.53 | 4.94 | 143.7 → 145.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 6.88 | 22.96 | 95% | 0.80 | 5.87 | 5.21 | 542.8 → 562.7 | 13 | 3.5 / 31 |

