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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.55 | 13.72 | 99% | 0.61 | 2.11 | 5.26 | 129.2 → 136.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 3.07 | 16.34 | 99% | 0.82 | 2.32 | 5.50 | 165.9 → 163.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 4.40 | 17.73 | 99% | 2.49 | 2.81 | 5.53 | 213.7 → 197.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 2.75 | 16.82 | 99% | 0.75 | 2.13 | 5.39 | 150.9 → 152.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 2.57 | 12.66 | 99% | 0.73 | 1.96 | 5.56 | 125.8 → 123.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.88 | 2.75 | 14.94 | 99% | 0.73 | 2.01 | 5.24 | 150.2 → 146.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 4.55 | 16.03 | 99% | 0.84 | 3.98 | 5.42 | 153.7 → 149.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.08 | 3.02 | 16.50 | 99% | 0.77 | 2.28 | 5.62 | 155.8 → 152.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 2.67 | 14.50 | 99% | 0.64 | 2.14 | 5.25 | 115.4 → 113.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.03 | 2.94 | 15.86 | 99% | 0.78 | 2.31 | 5.50 | 155.2 → 152.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.07 | 3.48 | 13.99 | 99% | 0.85 | 2.53 | 5.23 | 156.0 → 151.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.84 | 9.56 | 100% | 0.46 | 1.52 | 4.10 | 75.7 → 75.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.239 |
| ArrangingLayoutable LineDrawing | 0.066 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 2.50 | 14.77 | 99% | 0.70 | 1.98 | 5.24 | 151.0 → 152.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.49 | 8.47 | 39.23 | 95% | 1.09 | 7.07 | 7.89 | 563.1 → 584.9 | 14 | 3.5 / 31 |

