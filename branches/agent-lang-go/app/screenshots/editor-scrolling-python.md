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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.92 | 12.72 | 100% | 0.52 | 2.49 | 4.87 | 123.6 → 130.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.93 | 16.22 | 99% | 0.67 | 2.38 | 4.86 | 160.0 → 157.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 2.94 | 16.68 | 99% | 0.88 | 2.38 | 4.79 | 207.1 → 192.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 3.76 | 15.81 | 99% | 0.77 | 2.74 | 5.28 | 145.4 → 146.9 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.51 | 12.42 | 99% | 0.60 | 2.20 | 4.71 | 119.4 → 117.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.79 | 14.02 | 99% | 0.61 | 2.31 | 4.73 | 144.5 → 140.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.98 | 14.85 | 99% | 0.68 | 2.36 | 4.67 | 146.5 → 143.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.09 | 15.68 | 99% | 0.67 | 2.55 | 5.06 | 149.9 → 146.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.61 | 12.59 | 99% | 0.51 | 2.26 | 4.46 | 108.5 → 107.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.83 | 15.13 | 99% | 0.59 | 2.36 | 4.65 | 149.2 → 146.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 3.61 | 16.53 | 99% | 0.80 | 2.75 | 4.74 | 149.6 → 146.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.58 | 10.92 | 100% | 0.53 | 2.19 | 4.06 | 69.8 → 69.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.273 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.72 | 17.37 | 99% | 0.62 | 2.32 | 4.76 | 145.2 → 146.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.89 | 9.18 | 44.35 | 95% | 0.82 | 6.65 | 5.69 | 558.3 → 572.1 | 13 | 3.5 / 31 |

