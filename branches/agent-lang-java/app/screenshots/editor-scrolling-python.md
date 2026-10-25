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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.05 | 10.68 | 100% | 0.48 | 1.56 | 3.94 | 119.3 → 127.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 2.11 | 13.68 | 99% | 0.65 | 1.57 | 4.01 | 156.1 → 153.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 4.72 | 17.55 | 99% | 2.74 | 3.65 | 4.74 | 203.1 → 188.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.06 | 11.52 | 99% | 0.60 | 1.56 | 4.00 | 141.1 → 142.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.03 | 10.64 | 99% | 0.62 | 1.54 | 4.13 | 116.1 → 114.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.00 | 13.36 | 99% | 0.61 | 1.51 | 4.05 | 140.7 → 136.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.83 | 14.00 | 99% | 0.67 | 2.18 | 4.45 | 142.9 → 139.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 2.97 | 13.79 | 99% | 0.75 | 2.23 | 4.53 | 146.2 → 142.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 1.90 | 8.56 | 100% | 0.49 | 1.49 | 3.68 | 105.0 → 103.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.06 | 10.18 | 99% | 0.56 | 1.55 | 4.10 | 145.4 → 142.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.10 | 12.19 | 99% | 0.63 | 1.59 | 3.60 | 145.2 → 142.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.60 | 2.38 | 70.08 | 99% | 0.60 | 1.50 | 3.59 | 67.2 → 66.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.253 |
| AttachingStyle - | 0.033 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.15 | 11.73 | 99% | 0.63 | 1.63 | 4.04 | 141.6 → 142.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.76 | 4.41 | 20.64 | 96% | 0.65 | 3.65 | 4.30 | 544.2 → 569.5 | 12 | 3.5 / 31 |

