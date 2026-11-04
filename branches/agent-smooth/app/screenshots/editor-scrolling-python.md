# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.12 | 16.10 | 100% | 0.50 | 2.71 | 4.76 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 4.71 | 72.14 | 99% | 1.83 | 2.96 | 5.44 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.51 | 22.92 | 99% | 1.74 | 2.78 | 5.56 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 3.19 | 21.07 | 99% | 0.70 | 2.72 | 4.57 | 115.2 → 116.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.54 | 14.78 | 99% | 1.55 | 2.66 | 5.16 | 91.5 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.76 | 3.34 | 18.42 | 99% | 1.43 | 2.80 | 5.21 | 115.5 → 110.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.34 | 14.52 | 100% | 0.81 | 2.76 | 5.13 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 3.18 | 17.42 | 99% | 0.75 | 2.67 | 4.65 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.72 | 3.64 | 73.34 | 99% | 1.54 | 2.84 | 5.05 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 3.11 | 14.11 | 99% | 0.68 | 2.67 | 4.89 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.19 | 16.27 | 99% | 0.74 | 2.69 | 4.26 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.83 | 15.13 | 100% | 1.22 | 2.42 | 4.22 | 43.9 → 43.0 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.333 |
| AttachingStyle - | 0.100 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| ArrangingLayoutable TextView | 0.030 |
| ArrangingLayoutable Track | 0.018 |
| FindingResource - | 0.014 |
| MeasuringLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.31 | 39.92 | 99% | 0.65 | 2.76 | 5.04 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.53 | 10.46 | 83.30 | 92% | 1.82 | 8.01 | 9.86 | 606.2 → 632.0 | 15 | 3.5 / 30 |

