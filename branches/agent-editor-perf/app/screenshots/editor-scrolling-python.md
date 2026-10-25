# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
- 0 classified spans, 0 diagnostics, 683 folds, 0 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.13.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.48 | 5.50 | 35.43 | 96% | 0.43 | 5.01 | 8.70 | 273.7 → 313.5 | 9 | 3.5 / 31 |
| 4 flings at 5000 dip/s | 436 | 1.72 | 5.54 | 14.63 | 97% | 0.61 | 4.94 | 8.49 | 299.1 → 284.5 | 14 | 3.5 / 32 |
| 2 flings at 8000 dip/s | 308 | 2.19 | 5.94 | 15.66 | 96% | 0.94 | 5.12 | 9.11 | 371.6 → 344.9 | 11 | 5.8 / 32 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.58 | 5.49 | 15.75 | 97% | 0.55 | 4.92 | 8.65 | 286.7 → 292.3 | 12 | 3.5 / 31 |
| without line numbers | 368 | 1.46 | 4.50 | 26.60 | 98% | 0.57 | 3.71 | 8.09 | 212.6 → 217.8 | 10 | 3.9 / 60 |
| without the folding margin | 368 | 1.51 | 5.18 | 28.50 | 97% | 0.62 | 4.47 | 8.64 | 301.5 → 303.1 | 10 | 3.9 / 60 |
| without the breakpoint margin | 368 | 1.64 | 7.63 | 15.84 | 95% | 0.68 | 6.22 | 8.55 | 303.4 → 309.2 | 12 | 3.9 / 60 |
| without the Live Unit Testing margin | 368 | 1.59 | 5.88 | 16.73 | 96% | 0.57 | 5.12 | 8.70 | 309.9 → 310.5 | 12 | 3.9 / 60 |
| without coloring | 368 | 1.29 | 4.93 | 16.83 | 97% | 0.40 | 4.49 | 7.45 | 216.0 → 211.7 | 9 | 3.9 / 60 |
| without squiggles | 368 | 1.54 | 5.87 | 21.88 | 96% | 0.59 | 4.90 | 8.63 | 307.9 → 311.8 | 12 | 3.9 / 60 |
| without the floating touch bar | 368 | 1.52 | 5.56 | 16.86 | 97% | 0.57 | 4.77 | 7.29 | 306.3 → 310.7 | 11 | 3.9 / 60 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.87 | 3.30 | 18.02 | 98% | 0.38 | 2.89 | 6.11 | 110.9 → 106.8 | 6 | 3.9 / 60 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 182.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.711 |
| AttachingStyle - | 0.085 |
| ArrangingLayoutable TextView | 0.036 |
| FindingResource - | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable Track | 0.008 |
| MeasuringLayoutable LineDrawing | 0.005 |
| MeasuringLayoutable CachedLineNumberMargin | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Panel | 0.000 |

