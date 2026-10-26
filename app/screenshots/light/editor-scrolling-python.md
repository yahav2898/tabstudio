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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.59 | 17.53 | 99% | 0.54 | 2.16 | 3.54 | 121.9 → 129.5 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 4.44 | 14.50 | 99% | 2.30 | 2.74 | 3.87 | 158.1 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.02 | 14.87 | 99% | 0.85 | 2.35 | 3.80 | 204.2 → 189.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.48 | 13.77 | 99% | 0.63 | 2.01 | 3.52 | 143.2 → 144.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.04 | 10.18 | 99% | 0.62 | 1.56 | 3.37 | 117.5 → 115.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 2.25 | 12.97 | 99% | 0.66 | 1.68 | 3.67 | 142.3 → 138.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 3.39 | 16.09 | 99% | 0.72 | 2.58 | 4.03 | 144.5 → 141.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 2.99 | 16.39 | 99% | 0.74 | 2.14 | 4.02 | 148.2 → 144.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 2.16 | 12.60 | 99% | 0.53 | 1.65 | 3.28 | 107.0 → 105.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 2.17 | 13.96 | 99% | 0.61 | 1.71 | 3.56 | 147.0 → 144.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.28 | 13.56 | 99% | 0.65 | 1.71 | 3.20 | 146.7 → 143.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 1.81 | 11.43 | 100% | 0.57 | 1.42 | 2.85 | 68.5 → 67.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.283 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable LineDrawing | 0.023 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable TextView | 0.017 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.012 |
| MeasuringLayoutable Panel | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.82 | 17.23 | 99% | 0.87 | 2.56 | 3.92 | 143.9 → 144.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.79 | 4.93 | 22.59 | 96% | 0.61 | 3.76 | 3.66 | 544.4 → 561.1 | 12 | 3.5 / 31 |

