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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.27 | 10.31 | 99% | 0.43 | 1.92 | 3.14 | 123.3 → 131.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.40 | 13.66 | 99% | 0.60 | 1.92 | 3.33 | 159.9 → 157.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.89 | 13.73 | 99% | 0.80 | 2.14 | 3.63 | 206.9 → 192.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.77 | 18.66 | 99% | 0.68 | 2.11 | 3.66 | 145.2 → 147.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 3.05 | 19.42 | 99% | 0.65 | 2.35 | 3.42 | 120.2 → 118.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 1.93 | 12.67 | 99% | 0.51 | 1.67 | 3.01 | 144.2 → 140.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 2.24 | 15.11 | 99% | 0.57 | 1.84 | 3.35 | 146.9 → 143.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.06 | 12.49 | 99% | 0.51 | 1.76 | 3.20 | 149.7 → 146.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.28 | 11.48 | 100% | 0.54 | 1.87 | 3.35 | 109.0 → 107.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.45 | 13.19 | 99% | 0.54 | 1.96 | 3.43 | 149.1 → 146.6 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 3.22 | 12.79 | 99% | 0.59 | 2.48 | 3.09 | 149.7 → 146.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.56 | 8.35 | 100% | 0.43 | 1.24 | 2.53 | 70.3 → 70.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.185 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.44 | 13.68 | 99% | 0.61 | 1.93 | 4.00 | 145.5 → 146.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.64 | 5.44 | 21.91 | 96% | 0.58 | 4.23 | 3.71 | 549.0 → 569.4 | 13 | 3.5 / 31 |

