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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.81 | 2.42 | 12.19 | 99% | 0.52 | 1.97 | 4.16 | 121.9 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.03 | 3.26 | 15.31 | 99% | 0.75 | 2.60 | 4.40 | 158.0 → 156.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 4.64 | 16.19 | 99% | 2.74 | 2.75 | 4.62 | 205.2 → 190.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 3.12 | 14.40 | 99% | 0.70 | 2.53 | 4.18 | 143.6 → 144.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.86 | 14.07 | 99% | 0.70 | 2.44 | 4.33 | 118.5 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 3.33 | 17.22 | 99% | 0.80 | 2.68 | 4.83 | 143.1 → 139.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.02 | 3.34 | 15.55 | 99% | 0.82 | 2.63 | 4.59 | 145.9 → 142.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 2.93 | 11.96 | 99% | 0.71 | 2.19 | 4.38 | 148.0 → 144.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 3.03 | 9.71 | 100% | 0.57 | 2.52 | 4.04 | 107.6 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.03 | 3.32 | 15.37 | 99% | 0.76 | 2.71 | 4.50 | 147.7 → 145.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 3.50 | 15.15 | 99% | 0.77 | 2.75 | 4.12 | 148.4 → 144.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.53 | 11.26 | 99% | 0.54 | 2.26 | 3.44 | 69.2 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.09 | 16.74 | 99% | 0.68 | 2.57 | 4.25 | 143.7 → 144.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.30 | 8.89 | 80.01 | 95% | 2.59 | 6.62 | 4.51 | 549.6 → 560.7 | 14 | 3.5 / 31 |

