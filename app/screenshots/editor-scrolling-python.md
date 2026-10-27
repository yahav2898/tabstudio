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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.97 | 2.83 | 14.68 | 99% | 0.64 | 2.29 | 5.36 | 132.3 → 140.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.23 | 17.25 | 99% | 0.85 | 2.30 | 5.57 | 169.8 → 167.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.45 | 4.94 | 17.46 | 99% | 1.35 | 3.88 | 5.87 | 218.0 → 202.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 2.87 | 14.04 | 99% | 0.78 | 2.20 | 5.29 | 154.5 → 155.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 2.75 | 12.00 | 99% | 0.77 | 2.08 | 5.35 | 128.9 → 127.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.10 | 3.28 | 17.36 | 99% | 0.87 | 2.46 | 6.00 | 154.3 → 150.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.18 | 4.53 | 17.57 | 99% | 0.99 | 2.70 | 6.07 | 157.5 → 153.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.02 | 16.33 | 99% | 0.78 | 2.35 | 5.48 | 159.4 → 156.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 2.83 | 20.89 | 99% | 0.69 | 2.20 | 5.05 | 118.0 → 116.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.01 | 2.85 | 16.19 | 99% | 0.77 | 2.25 | 5.27 | 158.8 → 156.0 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 4.31 | 16.23 | 99% | 0.85 | 2.80 | 5.01 | 159.6 → 155.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.30 | 10.38 | 99% | 0.58 | 1.89 | 4.30 | 78.6 → 78.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.335 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 2.82 | 17.78 | 99% | 0.76 | 2.24 | 5.37 | 155.4 → 155.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.63 | 8.03 | 41.44 | 95% | 2.39 | 7.10 | 8.49 | 571.3 → 588.7 | 14 | 3.5 / 31 |

