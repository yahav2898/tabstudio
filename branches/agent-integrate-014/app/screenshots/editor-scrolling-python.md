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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.08 | 13.75 | 99% | 0.48 | 2.70 | 4.80 | 122.6 → 130.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.23 | 15.75 | 99% | 0.66 | 2.72 | 5.18 | 159.0 → 156.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 4.00 | 80.30 | 98% | 2.22 | 2.90 | 5.54 | 205.9 → 191.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.10 | 12.83 | 99% | 0.64 | 2.62 | 5.06 | 144.4 → 145.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 3.02 | 22.84 | 99% | 0.67 | 2.49 | 5.14 | 119.0 → 117.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 3.27 | 18.94 | 99% | 0.68 | 2.63 | 5.00 | 143.6 → 140.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.91 | 3.49 | 14.91 | 99% | 0.69 | 2.87 | 5.33 | 146.9 → 142.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.42 | 14.12 | 99% | 0.67 | 2.81 | 5.04 | 148.9 → 145.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.97 | 12.81 | 99% | 0.49 | 2.61 | 4.85 | 108.2 → 106.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 5.03 | 74.42 | 99% | 0.82 | 3.16 | 5.27 | 149.5 → 146.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.28 | 12.88 | 99% | 0.66 | 2.78 | 4.27 | 148.4 → 145.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.72 | 10.92 | 100% | 0.46 | 2.39 | 4.13 | 70.2 → 69.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.311 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.51 | 76.54 | 99% | 0.78 | 2.88 | 5.35 | 145.8 → 145.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 5.82 | 32.83 | 96% | 0.74 | 5.17 | 5.17 | 548.4 → 566.4 | 13 | 3.5 / 31 |

