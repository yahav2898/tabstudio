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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 1.92 | 12.40 | 99% | 0.47 | 1.52 | 3.97 | 122.4 → 130.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 3.24 | 17.12 | 99% | 0.77 | 2.32 | 4.33 | 159.1 → 157.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.63 | 14.51 | 99% | 1.04 | 1.79 | 4.46 | 206.5 → 191.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 1.99 | 12.62 | 99% | 0.62 | 1.54 | 4.02 | 144.3 → 145.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 1.95 | 14.02 | 99% | 0.64 | 1.48 | 4.13 | 118.7 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 1.97 | 16.17 | 99% | 0.62 | 1.54 | 4.02 | 143.5 → 139.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.89 | 3.64 | 16.99 | 99% | 0.83 | 2.49 | 4.60 | 146.2 → 143.1 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 2.09 | 15.22 | 99% | 0.61 | 1.63 | 4.04 | 148.7 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 1.99 | 11.13 | 99% | 0.50 | 1.58 | 3.82 | 107.4 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.24 | 13.84 | 99% | 0.64 | 1.67 | 4.19 | 149.0 → 145.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.84 | 2.49 | 13.29 | 99% | 0.65 | 1.70 | 3.79 | 148.4 → 145.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 2.56 | 13.29 | 100% | 0.53 | 2.21 | 3.46 | 69.0 → 68.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| ArrangingLayoutable ScrollingTextLayer | 0.050 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.16 | 17.31 | 99% | 0.63 | 1.65 | 4.15 | 144.7 → 145.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.04 | 4.39 | 29.27 | 96% | 0.70 | 3.70 | 4.62 | 552.1 → 570.4 | 13 | 3.5 / 31 |

