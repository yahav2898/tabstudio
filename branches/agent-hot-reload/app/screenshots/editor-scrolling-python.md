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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 2.58 | 11.31 | 99% | 0.57 | 2.05 | 4.76 | 123.0 → 130.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.18 | 3.97 | 14.65 | 99% | 0.87 | 2.71 | 5.44 | 160.9 → 157.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.28 | 3.10 | 15.59 | 99% | 1.06 | 2.16 | 5.34 | 207.7 → 192.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.70 | 15.47 | 99% | 0.80 | 2.02 | 5.28 | 145.2 → 146.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 4.32 | 12.79 | 99% | 1.21 | 2.56 | 6.05 | 119.9 → 118.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 3.21 | 17.13 | 99% | 0.80 | 2.62 | 5.01 | 145.1 → 141.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 3.34 | 16.89 | 99% | 0.78 | 2.66 | 4.99 | 147.1 → 143.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.13 | 3.55 | 14.46 | 99% | 0.85 | 2.86 | 5.07 | 149.9 → 147.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.94 | 3.06 | 10.87 | 100% | 0.60 | 2.57 | 4.69 | 108.9 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 3.34 | 15.10 | 99% | 0.79 | 2.68 | 5.11 | 150.1 → 146.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.02 | 3.31 | 14.70 | 99% | 0.83 | 2.68 | 4.67 | 149.3 → 146.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 2.81 | 15.05 | 99% | 0.61 | 2.34 | 4.22 | 70.3 → 69.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.261 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 3.26 | 19.37 | 99% | 0.78 | 2.61 | 5.08 | 145.9 → 146.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.94 | 7.70 | 30.78 | 95% | 1.05 | 6.86 | 7.04 | 561.3 → 574.3 | 13 | 3.5 / 31 |

