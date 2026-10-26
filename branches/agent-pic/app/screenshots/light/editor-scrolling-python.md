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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 2.53 | 12.46 | 99% | 0.57 | 2.00 | 4.80 | 123.0 → 130.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 4.32 | 15.72 | 99% | 2.50 | 2.41 | 5.26 | 159.9 → 158.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 3.09 | 16.08 | 99% | 1.09 | 2.13 | 5.31 | 207.0 → 191.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 2.49 | 12.54 | 99% | 0.69 | 1.92 | 4.74 | 144.5 → 145.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 2.36 | 10.85 | 99% | 0.73 | 1.76 | 4.88 | 119.0 → 117.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.92 | 4.26 | 15.08 | 99% | 0.91 | 2.72 | 5.14 | 144.8 → 140.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 2.66 | 14.96 | 99% | 0.75 | 1.99 | 4.91 | 146.4 → 142.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 2.60 | 21.10 | 99% | 0.72 | 1.97 | 4.99 | 149.1 → 145.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 2.87 | 77.46 | 99% | 0.64 | 1.96 | 4.94 | 108.3 → 106.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 2.80 | 13.82 | 99% | 0.78 | 2.00 | 5.06 | 149.2 → 146.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 2.44 | 14.12 | 99% | 0.76 | 1.93 | 4.41 | 148.9 → 145.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 2.03 | 8.74 | 100% | 0.57 | 1.58 | 4.06 | 69.0 → 68.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.333 |
| ArrangingLayoutable LineDrawing | 0.023 |
| ArrangingLayoutable TextView | 0.022 |
| RaisingRoutedEvent ScrollBar | 0.016 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.15 | 3.40 | 77.36 | 99% | 0.85 | 2.54 | 5.30 | 145.9 → 146.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.67 | 5.81 | 26.19 | 96% | 0.97 | 4.87 | 5.52 | 544.5 → 568.7 | 12 | 3.5 / 31 |

