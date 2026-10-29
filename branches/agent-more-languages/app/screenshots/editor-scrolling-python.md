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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.18 | 3.99 | 14.56 | 99% | 0.67 | 3.28 | 6.34 | 136.0 → 144.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.38 | 4.34 | 94.98 | 98% | 0.98 | 3.30 | 6.30 | 173.8 → 170.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.54 | 4.20 | 19.91 | 99% | 1.10 | 3.24 | 6.34 | 220.9 → 205.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.28 | 3.82 | 15.15 | 99% | 0.79 | 3.09 | 5.99 | 158.2 → 159.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.12 | 3.90 | 12.91 | 99% | 0.91 | 2.97 | 6.05 | 132.7 → 130.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.27 | 4.19 | 15.97 | 99% | 0.90 | 3.15 | 6.26 | 157.8 → 153.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.28 | 3.84 | 20.45 | 99% | 0.83 | 3.11 | 6.08 | 160.0 → 156.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.25 | 3.81 | 15.17 | 99% | 0.81 | 3.08 | 6.15 | 162.1 → 158.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.16 | 3.77 | 13.40 | 99% | 0.72 | 3.11 | 5.97 | 121.2 → 119.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.31 | 4.01 | 16.96 | 99% | 0.85 | 3.19 | 6.49 | 163.0 → 159.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.25 | 3.96 | 17.61 | 99% | 0.82 | 3.20 | 5.70 | 161.8 → 159.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.87 | 3.24 | 12.38 | 99% | 0.62 | 2.68 | 5.00 | 83.3 → 82.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.39 | 5.51 | 17.94 | 99% | 0.94 | 3.28 | 6.51 | 159.1 → 160.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.79 | 6.62 | 27.16 | 96% | 0.89 | 5.86 | 6.73 | 571.5 → 590.3 | 13 | 3.5 / 31 |

