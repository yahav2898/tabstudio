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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 2.75 | 13.01 | 99% | 0.61 | 2.19 | 5.14 | 123.2 → 130.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.21 | 3.26 | 14.87 | 99% | 0.87 | 2.31 | 5.31 | 160.3 → 158.7 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.60 | 15.62 | 99% | 1.29 | 2.34 | 5.66 | 208.4 → 192.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.12 | 3.00 | 14.99 | 99% | 0.81 | 2.24 | 5.25 | 145.4 → 146.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 2.66 | 14.28 | 99% | 0.79 | 1.98 | 5.07 | 119.8 → 117.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.20 | 3.88 | 16.88 | 99% | 2.53 | 2.52 | 5.50 | 145.4 → 141.6 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 2.80 | 18.30 | 99% | 0.80 | 2.14 | 5.28 | 147.0 → 143.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.09 | 2.88 | 15.18 | 99% | 0.83 | 2.19 | 5.68 | 150.1 → 146.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 2.60 | 12.41 | 99% | 0.63 | 2.00 | 4.95 | 108.7 → 107.0 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 4.82 | 14.76 | 99% | 1.09 | 2.85 | 5.60 | 150.7 → 146.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.13 | 2.89 | 14.70 | 99% | 0.83 | 2.20 | 4.90 | 149.8 → 146.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.73 | 2.34 | 10.09 | 100% | 0.66 | 1.78 | 4.47 | 70.3 → 69.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.349 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.23 | 3.45 | 21.46 | 99% | 0.89 | 2.52 | 5.47 | 146.5 → 147.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.54 | 5.56 | 32.72 | 96% | 0.91 | 4.70 | 5.39 | 551.3 → 572.3 | 13 | 3.5 / 31 |

