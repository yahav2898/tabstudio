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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 1.96 | 16.37 | 99% | 0.39 | 1.67 | 4.21 | 131.2 → 138.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.22 | 14.75 | 99% | 0.60 | 1.76 | 4.54 | 167.6 → 165.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 2.66 | 16.38 | 99% | 0.95 | 1.99 | 5.23 | 214.2 → 199.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 4.45 | 18.42 | 99% | 0.93 | 3.82 | 5.43 | 153.5 → 155.2 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.00 | 19.59 | 99% | 0.56 | 1.63 | 4.83 | 127.4 → 126.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.13 | 12.61 | 99% | 0.61 | 1.73 | 4.70 | 152.5 → 148.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.16 | 14.50 | 99% | 0.61 | 1.76 | 4.92 | 154.7 → 151.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 3.20 | 14.37 | 99% | 0.66 | 2.05 | 4.94 | 157.4 → 154.7 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.97 | 12.45 | 99% | 0.50 | 2.50 | 4.84 | 117.4 → 115.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.19 | 18.42 | 99% | 0.60 | 1.79 | 5.06 | 157.1 → 154.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.17 | 14.33 | 99% | 0.58 | 1.76 | 4.12 | 156.8 → 153.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.68 | 10.87 | 99% | 0.45 | 1.43 | 3.99 | 77.9 → 77.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.232 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 4.04 | 76.10 | 99% | 0.76 | 2.72 | 5.05 | 154.1 → 154.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.24 | 5.07 | 23.45 | 96% | 0.74 | 4.08 | 5.33 | 561.7 → 584.8 | 13 | 3.5 / 31 |

