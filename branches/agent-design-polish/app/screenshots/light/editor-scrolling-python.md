# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.79 | 12.27 | 99% | 0.58 | 2.28 | 5.37 | 129.2 → 136.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 2.84 | 16.48 | 99% | 0.79 | 2.17 | 5.33 | 165.1 → 163.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 3.61 | 18.50 | 99% | 2.62 | 2.45 | 5.81 | 212.2 → 198.1 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.07 | 3.31 | 13.74 | 99% | 0.82 | 2.57 | 5.63 | 151.9 → 151.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 2.70 | 17.24 | 99% | 0.77 | 2.08 | 5.28 | 125.4 → 123.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.90 | 19.89 | 99% | 0.78 | 2.17 | 5.36 | 149.9 → 146.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 3.38 | 18.66 | 99% | 0.84 | 2.57 | 5.69 | 152.3 → 149.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.19 | 3.52 | 15.95 | 99% | 0.84 | 2.66 | 6.07 | 156.0 → 152.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 2.87 | 11.79 | 100% | 0.66 | 2.30 | 5.14 | 115.0 → 113.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.14 | 3.08 | 15.95 | 99% | 0.74 | 2.40 | 5.56 | 155.4 → 152.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.12 | 3.51 | 18.74 | 99% | 0.91 | 2.60 | 5.25 | 154.7 → 152.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.62 | 11.58 | 99% | 0.62 | 2.03 | 4.45 | 76.8 → 75.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| AttachingStyle - | 0.056 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.21 | 3.18 | 17.36 | 100% | 0.88 | 2.43 | 5.71 | 151.6 → 153.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.88 | 7.57 | 31.88 | 95% | 0.99 | 6.43 | 6.48 | 552.3 → 576.9 | 14 | 3.5 / 31 |

