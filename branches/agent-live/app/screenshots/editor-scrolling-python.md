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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.49 | 13.72 | 99% | 0.54 | 2.03 | 4.86 | 129.8 → 137.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.03 | 2.69 | 15.96 | 99% | 0.71 | 2.07 | 5.22 | 167.1 → 164.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.56 | 4.09 | 18.84 | 99% | 1.26 | 2.75 | 6.27 | 215.7 → 200.6 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 2.82 | 16.31 | 99% | 0.74 | 2.15 | 5.39 | 153.0 → 153.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.45 | 14.22 | 99% | 0.73 | 1.91 | 5.60 | 126.5 → 124.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.76 | 17.95 | 99% | 0.74 | 2.10 | 5.51 | 151.9 → 147.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.02 | 3.37 | 17.75 | 99% | 0.77 | 2.48 | 5.31 | 153.6 → 151.2 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.03 | 3.14 | 12.31 | 99% | 0.79 | 2.29 | 5.68 | 157.1 → 153.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 2.45 | 15.44 | 99% | 0.56 | 1.97 | 5.20 | 115.5 → 113.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 2.77 | 18.92 | 99% | 0.75 | 2.14 | 5.31 | 156.7 → 153.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.04 | 3.49 | 17.81 | 99% | 0.84 | 2.63 | 4.87 | 156.1 → 153.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.53 | 9.91 | 99% | 0.54 | 1.85 | 4.31 | 76.9 → 75.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.275 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.07 | 2.78 | 18.05 | 99% | 0.78 | 2.13 | 5.26 | 153.2 → 153.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.49 | 8.17 | 33.37 | 95% | 2.52 | 6.86 | 6.26 | 564.0 → 587.2 | 14 | 3.5 / 31 |

