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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.03 | 12.86 | 99% | 0.45 | 1.69 | 3.87 | 121.5 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.28 | 85.82 | 99% | 0.80 | 2.45 | 4.30 | 157.9 → 155.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 2.46 | 15.15 | 99% | 0.87 | 1.78 | 4.34 | 204.0 → 189.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.78 | 14.49 | 99% | 0.48 | 1.41 | 3.73 | 142.5 → 143.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 1.85 | 12.76 | 99% | 0.53 | 1.42 | 3.84 | 117.5 → 115.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.92 | 16.56 | 99% | 0.56 | 1.49 | 4.04 | 142.3 → 138.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.74 | 14.48 | 99% | 0.61 | 1.86 | 4.20 | 144.5 → 141.5 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 2.78 | 85.07 | 99% | 0.66 | 2.14 | 4.31 | 148.0 → 144.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.13 | 11.78 | 99% | 0.50 | 1.67 | 3.93 | 107.2 → 105.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 2.19 | 14.20 | 99% | 0.61 | 1.68 | 4.00 | 147.2 → 144.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.17 | 14.49 | 99% | 0.58 | 1.65 | 3.76 | 147.1 → 143.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.53 | 10.08 | 100% | 0.38 | 1.23 | 3.31 | 68.2 → 67.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable TextView | 0.074 |
| ArrangingLayoutable Track | 0.027 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.77 | 14.04 | 99% | 0.59 | 2.33 | 4.10 | 143.8 → 144.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 5.19 | 26.20 | 96% | 0.81 | 4.34 | 4.94 | 547.2 → 563.6 | 13 | 3.5 / 31 |

