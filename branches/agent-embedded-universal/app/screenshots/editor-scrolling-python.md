# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.31 | 12.34 | 99% | 0.50 | 2.87 | 4.57 | 131.5 → 139.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.70 | 14.38 | 99% | 0.75 | 3.04 | 5.09 | 168.3 → 167.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.13 | 4.27 | 100.76 | 99% | 1.10 | 3.33 | 5.70 | 215.6 → 200.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 3.42 | 15.42 | 99% | 0.68 | 2.91 | 5.18 | 154.0 → 154.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.28 | 14.52 | 99% | 0.64 | 2.75 | 4.96 | 128.4 → 126.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.99 | 4.04 | 18.88 | 99% | 1.85 | 3.17 | 5.43 | 153.4 → 150.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 3.48 | 15.78 | 99% | 0.64 | 2.96 | 4.88 | 155.0 → 151.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.46 | 14.99 | 99% | 0.68 | 2.93 | 4.93 | 158.1 → 154.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.28 | 10.46 | 99% | 0.51 | 2.88 | 4.77 | 117.2 → 115.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.05 | 5.42 | 17.64 | 99% | 0.78 | 4.96 | 5.37 | 159.5 → 155.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 3.38 | 16.05 | 99% | 0.63 | 2.91 | 4.43 | 157.4 → 154.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.88 | 11.65 | 99% | 0.48 | 2.57 | 4.07 | 79.2 → 78.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.251 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 5.42 | 19.05 | 99% | 2.57 | 3.51 | 5.78 | 154.6 → 156.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 6.74 | 23.25 | 95% | 0.73 | 5.81 | 5.09 | 557.1 → 578.4 | 13 | 3.5 / 31 |

