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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.09 | 2.99 | 12.28 | 99% | 0.66 | 2.32 | 5.35 | 124.0 → 131.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 3.85 | 15.77 | 99% | 0.90 | 2.55 | 5.51 | 161.0 → 158.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.38 | 3.19 | 16.71 | 99% | 1.09 | 2.25 | 5.63 | 208.2 → 193.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 2.80 | 13.08 | 99% | 0.79 | 2.13 | 5.21 | 145.4 → 146.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.00 | 3.58 | 12.32 | 99% | 0.92 | 2.45 | 5.31 | 120.1 → 118.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.61 | 15.95 | 99% | 0.76 | 2.00 | 5.19 | 144.8 → 140.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 2.94 | 15.71 | 99% | 0.84 | 2.12 | 5.40 | 147.2 → 143.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 4.65 | 72.74 | 99% | 0.99 | 2.65 | 5.69 | 149.8 → 147.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.96 | 2.66 | 10.30 | 99% | 0.65 | 2.06 | 4.92 | 108.6 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 2.91 | 14.23 | 99% | 0.84 | 2.17 | 5.31 | 149.9 → 146.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 3.15 | 14.71 | 99% | 0.84 | 2.18 | 4.87 | 149.7 → 146.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.38 | 8.30 | 100% | 0.61 | 1.81 | 4.44 | 70.1 → 69.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.325 |
| ArrangingLayoutable ScrollingTextLayer | 0.053 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 2.89 | 17.89 | 99% | 0.84 | 2.23 | 5.46 | 146.2 → 147.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.49 | 9.62 | 30.23 | 93% | 2.67 | 8.24 | 6.77 | 565.7 → 571.3 | 14 | 3.5 / 31 |

