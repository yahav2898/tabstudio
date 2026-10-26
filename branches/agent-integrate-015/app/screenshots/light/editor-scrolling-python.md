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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.59 | 12.89 | 99% | 0.40 | 2.30 | 4.40 | 126.6 → 133.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.82 | 15.75 | 99% | 0.64 | 2.39 | 4.85 | 162.5 → 160.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.24 | 5.26 | 17.13 | 99% | 2.84 | 3.03 | 5.60 | 210.2 → 195.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.81 | 16.22 | 99% | 0.60 | 2.34 | 4.50 | 147.9 → 149.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.40 | 11.87 | 99% | 0.58 | 2.08 | 5.02 | 123.0 → 121.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 2.82 | 15.86 | 99% | 0.62 | 2.46 | 4.65 | 147.5 → 143.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 5.06 | 83.57 | 99% | 0.96 | 2.88 | 5.14 | 150.0 → 147.3 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.84 | 15.31 | 99% | 0.59 | 2.42 | 4.63 | 152.3 → 149.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.60 | 13.83 | 99% | 0.48 | 2.29 | 4.61 | 112.0 → 110.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 2.64 | 17.58 | 99% | 0.72 | 2.10 | 5.01 | 152.0 → 149.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.94 | 3.89 | 18.26 | 99% | 0.81 | 2.83 | 4.71 | 152.8 → 149.9 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.69 | 10.34 | 100% | 0.52 | 2.34 | 4.07 | 74.2 → 73.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| ArrangingLayoutable TextView | 0.065 |
| AttachingStyle - | 0.059 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.12 | 19.14 | 99% | 0.69 | 2.60 | 5.08 | 148.7 → 149.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.49 | 7.43 | 25.67 | 96% | 0.81 | 6.06 | 5.49 | 549.8 → 566.8 | 14 | 3.5 / 31 |

