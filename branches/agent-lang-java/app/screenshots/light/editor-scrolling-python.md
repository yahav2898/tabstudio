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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 2.14 | 11.19 | 99% | 0.48 | 1.63 | 3.35 | 122.0 → 129.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 3.65 | 16.86 | 99% | 2.57 | 2.50 | 3.74 | 159.0 → 156.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.33 | 19.80 | 99% | 0.78 | 1.65 | 3.65 | 206.0 → 190.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 2.18 | 13.88 | 99% | 0.64 | 1.67 | 3.65 | 143.8 → 145.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 1.94 | 12.30 | 99% | 0.63 | 1.43 | 3.33 | 117.9 → 116.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.10 | 13.37 | 99% | 0.65 | 1.56 | 3.50 | 143.4 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 3.36 | 73.98 | 99% | 2.33 | 2.52 | 3.54 | 146.0 → 142.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 2.36 | 16.65 | 99% | 0.66 | 1.75 | 3.48 | 148.9 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 1.88 | 9.69 | 99% | 0.50 | 1.46 | 3.07 | 107.1 → 105.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 2.00 | 13.49 | 99% | 0.61 | 1.57 | 3.36 | 147.9 → 145.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 1.99 | 12.64 | 99% | 0.59 | 1.51 | 2.92 | 147.8 → 144.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 1.89 | 9.51 | 100% | 0.52 | 1.33 | 2.90 | 68.2 → 67.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.410 |
| ArrangingLayoutable CachedLineNumberMargin | 0.041 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable LineDrawing | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable ContentPresenter | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable Border | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 2.11 | 13.49 | 99% | 0.61 | 1.57 | 3.30 | 144.0 → 144.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.85 | 4.21 | 26.46 | 96% | 0.64 | 3.57 | 3.55 | 546.2 → 563.8 | 13 | 3.5 / 31 |

