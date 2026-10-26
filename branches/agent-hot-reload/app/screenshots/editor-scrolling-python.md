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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.06 | 12.21 | 99% | 0.43 | 1.75 | 4.35 | 122.6 → 129.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.92 | 14.97 | 99% | 0.80 | 2.52 | 5.33 | 158.4 → 156.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.50 | 14.21 | 99% | 0.86 | 1.81 | 4.97 | 205.1 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.22 | 13.43 | 99% | 0.57 | 1.76 | 4.87 | 143.6 → 145.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 1.87 | 13.11 | 99% | 0.56 | 1.53 | 4.75 | 118.5 → 116.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 3.12 | 17.96 | 99% | 0.83 | 2.10 | 5.30 | 143.2 → 140.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.60 | 11.07 | 99% | 0.61 | 2.00 | 4.92 | 145.7 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.21 | 15.58 | 99% | 0.60 | 1.78 | 4.51 | 148.3 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.94 | 9.33 | 99% | 0.42 | 1.63 | 4.22 | 108.0 → 106.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.63 | 11.34 | 99% | 0.66 | 1.94 | 4.85 | 148.0 → 145.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.84 | 71.38 | 99% | 0.64 | 2.07 | 4.30 | 148.5 → 144.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.69 | 8.17 | 100% | 0.39 | 1.41 | 3.90 | 69.1 → 68.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.198 |
| ArrangingLayoutable CachedLineNumberMargin | 0.046 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.29 | 14.82 | 99% | 0.59 | 1.84 | 4.73 | 144.1 → 145.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.01 | 8.45 | 41.78 | 95% | 2.52 | 5.85 | 5.66 | 552.2 → 566.5 | 14 | 3.5 / 31 |

