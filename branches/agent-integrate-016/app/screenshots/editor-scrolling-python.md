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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 2.37 | 13.44 | 99% | 0.55 | 1.97 | 4.89 | 129.2 → 136.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.05 | 2.96 | 14.64 | 99% | 0.80 | 2.18 | 5.20 | 165.4 → 163.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 4.60 | 15.38 | 99% | 2.33 | 2.73 | 5.78 | 213.5 → 198.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.07 | 2.83 | 16.95 | 99% | 0.79 | 2.15 | 5.27 | 151.5 → 152.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 2.43 | 12.29 | 99% | 0.74 | 1.92 | 5.13 | 125.6 → 123.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.94 | 2.81 | 16.18 | 99% | 0.75 | 2.14 | 5.20 | 150.6 → 146.3 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 3.86 | 8.48 | 100% | 0.91 | 2.58 | 5.54 | 153.3 → 149.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.03 | 2.75 | 16.40 | 99% | 0.77 | 2.08 | 5.50 | 155.3 → 152.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 2.56 | 15.48 | 99% | 0.58 | 2.03 | 4.87 | 115.0 → 113.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.98 | 2.81 | 16.38 | 99% | 0.80 | 2.15 | 5.14 | 155.2 → 152.4 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.04 | 4.53 | 18.21 | 99% | 0.92 | 3.94 | 4.89 | 155.4 → 152.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 1.95 | 10.66 | 99% | 0.48 | 1.63 | 4.00 | 76.1 → 75.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.237 |
| ArrangingLayoutable ScrollingTextLayer | 0.073 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
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
| this editor | 368 | 0.91 | 2.54 | 13.73 | 99% | 0.70 | 2.01 | 4.90 | 150.8 → 152.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.50 | 9.79 | 87.45 | 93% | 2.45 | 7.26 | 6.26 | 557.8 → 578.5 | 14 | 3.5 / 31 |

