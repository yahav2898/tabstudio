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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 2.34 | 11.54 | 99% | 0.49 | 1.96 | 4.61 | 126.6 → 134.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 2.49 | 13.14 | 99% | 0.69 | 1.92 | 4.99 | 163.2 → 160.7 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.26 | 4.86 | 17.33 | 99% | 2.69 | 2.73 | 5.72 | 210.7 → 195.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.29 | 14.79 | 99% | 0.65 | 1.81 | 5.16 | 148.3 → 149.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.19 | 12.17 | 99% | 0.64 | 1.71 | 5.00 | 123.1 → 121.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.31 | 18.36 | 99% | 0.63 | 1.84 | 4.89 | 148.0 → 144.0 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.96 | 3.87 | 16.52 | 99% | 2.21 | 2.32 | 5.23 | 150.1 → 147.5 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.91 | 2.73 | 89.59 | 99% | 0.72 | 2.08 | 5.21 | 153.2 → 149.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 2.19 | 11.54 | 99% | 0.50 | 1.84 | 5.12 | 112.6 → 110.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.31 | 14.85 | 99% | 0.64 | 1.84 | 4.87 | 152.6 → 149.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.36 | 14.15 | 99% | 0.61 | 1.82 | 4.12 | 152.5 → 149.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.29 | 76.80 | 99% | 0.53 | 1.68 | 4.04 | 74.0 → 73.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.259 |
| ArrangingLayoutable LineDrawing | 0.046 |
| ArrangingLayoutable Grid | 0.039 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.18 | 15.58 | 99% | 0.59 | 1.77 | 4.87 | 148.6 → 149.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 5.37 | 31.83 | 96% | 0.75 | 4.58 | 5.24 | 552.3 → 573.1 | 13 | 3.5 / 31 |

