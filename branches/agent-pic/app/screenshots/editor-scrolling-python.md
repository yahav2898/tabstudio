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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.52 | 13.28 | 99% | 0.57 | 2.04 | 5.21 | 123.2 → 130.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.32 | 4.12 | 17.82 | 99% | 0.91 | 2.92 | 5.90 | 160.5 → 159.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.24 | 3.17 | 17.56 | 99% | 1.15 | 2.27 | 5.68 | 207.9 → 192.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 2.54 | 15.41 | 99% | 0.70 | 2.01 | 4.94 | 145.0 → 146.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 2.35 | 12.97 | 99% | 0.69 | 1.79 | 4.94 | 119.6 → 117.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.12 | 3.72 | 77.30 | 99% | 0.98 | 2.77 | 5.65 | 145.1 → 141.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.90 | 2.64 | 13.13 | 99% | 0.74 | 1.92 | 5.19 | 147.1 → 143.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.93 | 2.76 | 13.96 | 99% | 0.81 | 2.08 | 6.01 | 149.5 → 146.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 2.64 | 13.05 | 99% | 0.66 | 2.09 | 5.16 | 108.8 → 107.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.32 | 3.68 | 79.64 | 99% | 0.93 | 2.70 | 6.37 | 151.0 → 147.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 2.90 | 17.55 | 99% | 0.82 | 2.16 | 4.88 | 149.5 → 146.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.14 | 14.32 | 99% | 0.59 | 1.69 | 4.16 | 70.1 → 69.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| MeasuringLayoutable LineDrawing | 0.039 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.30 | 3.73 | 18.62 | 99% | 0.95 | 2.75 | 5.71 | 146.2 → 147.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.55 | 5.57 | 25.90 | 96% | 0.87 | 4.70 | 5.46 | 553.4 → 571.1 | 12 | 3.5 / 31 |

