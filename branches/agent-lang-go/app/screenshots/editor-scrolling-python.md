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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 2.69 | 13.20 | 99% | 0.61 | 2.12 | 5.16 | 123.1 → 130.8 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 4.17 | 14.20 | 99% | 0.90 | 2.72 | 5.49 | 161.0 → 158.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.28 | 3.22 | 15.00 | 99% | 1.11 | 2.20 | 5.50 | 207.7 → 193.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 2.80 | 14.60 | 99% | 0.82 | 2.13 | 5.23 | 145.2 → 146.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 3.22 | 15.13 | 99% | 0.89 | 2.47 | 5.61 | 120.1 → 118.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.07 | 2.77 | 12.92 | 99% | 0.78 | 2.08 | 5.24 | 145.0 → 141.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 2.79 | 14.15 | 99% | 0.81 | 2.14 | 5.28 | 147.3 → 143.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.18 | 4.46 | 14.10 | 99% | 0.93 | 2.72 | 6.46 | 150.3 → 147.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 2.57 | 10.50 | 100% | 0.63 | 2.05 | 4.91 | 108.8 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 2.90 | 13.84 | 99% | 0.80 | 2.16 | 5.24 | 149.9 → 146.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 3.34 | 19.32 | 99% | 0.84 | 2.30 | 4.78 | 149.6 → 146.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.70 | 2.46 | 12.56 | 100% | 0.64 | 1.77 | 4.36 | 70.3 → 69.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.288 |
| ArrangingLayoutable ScrollingTextLayer | 0.049 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 2.88 | 17.79 | 99% | 0.78 | 2.23 | 5.23 | 145.7 → 146.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.56 | 9.53 | 30.97 | 94% | 2.56 | 6.61 | 5.70 | 555.7 → 574.9 | 13 | 3.5 / 31 |

