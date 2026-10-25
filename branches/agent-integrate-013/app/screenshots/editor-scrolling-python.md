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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.04 | 11.18 | 99% | 0.34 | 1.74 | 3.37 | 122.2 → 129.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 1.95 | 13.97 | 99% | 0.50 | 1.63 | 3.81 | 159.2 → 156.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 3.49 | 15.08 | 99% | 0.92 | 2.18 | 4.09 | 206.8 → 191.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.64 | 73.48 | 99% | 0.57 | 2.13 | 3.92 | 144.8 → 145.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.03 | 11.49 | 99% | 0.48 | 1.69 | 3.67 | 118.6 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.71 | 13.43 | 99% | 0.53 | 1.36 | 3.76 | 143.4 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 1.67 | 12.52 | 99% | 0.50 | 1.33 | 3.74 | 145.7 → 142.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.88 | 13.98 | 99% | 0.61 | 1.60 | 3.91 | 148.7 → 145.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.82 | 70.02 | 99% | 0.40 | 1.36 | 3.56 | 108.0 → 105.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.54 | 1.52 | 12.67 | 99% | 0.47 | 1.22 | 3.63 | 148.0 → 145.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 1.64 | 10.37 | 99% | 0.50 | 1.31 | 3.34 | 148.2 → 144.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.23 | 6.04 | 100% | 0.32 | 1.00 | 3.05 | 68.5 → 67.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.151 |
| ArrangingLayoutable CachedLineNumberMargin | 0.055 |
| AttachingStyle - | 0.041 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.00 | 13.84 | 99% | 0.62 | 2.42 | 3.83 | 144.5 → 145.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.60 | 3.88 | 20.42 | 96% | 0.60 | 3.30 | 3.89 | 549.2 → 569.2 | 13 | 3.5 / 31 |

