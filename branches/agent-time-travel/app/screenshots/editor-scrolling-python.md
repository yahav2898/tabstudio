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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 3.24 | 13.70 | 99% | 0.58 | 2.72 | 5.21 | 123.8 → 131.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 3.69 | 16.02 | 99% | 0.89 | 2.86 | 5.75 | 161.3 → 159.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.37 | 3.70 | 16.53 | 99% | 1.11 | 2.83 | 5.57 | 208.3 → 193.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.49 | 13.37 | 99% | 0.81 | 2.83 | 5.49 | 146.2 → 147.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.02 | 3.49 | 15.83 | 99% | 0.83 | 2.71 | 5.45 | 120.4 → 119.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.16 | 3.43 | 14.79 | 99% | 0.85 | 2.77 | 5.44 | 145.9 → 141.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.21 | 3.62 | 18.06 | 99% | 0.85 | 2.85 | 5.62 | 148.2 → 144.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.27 | 3.71 | 17.58 | 99% | 0.91 | 3.01 | 5.27 | 151.1 → 147.9 | 5 | 1.2 / 30 |
| without coloring | 368 | 1.04 | 3.48 | 11.63 | 100% | 0.68 | 2.81 | 5.14 | 109.6 → 107.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.21 | 3.60 | 14.36 | 99% | 0.85 | 2.88 | 5.44 | 150.5 → 147.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.21 | 3.61 | 16.57 | 99% | 0.87 | 2.94 | 5.12 | 150.3 → 147.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.75 | 3.05 | 13.14 | 99% | 0.68 | 2.51 | 4.62 | 70.6 → 69.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.332 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.18 | 3.46 | 20.61 | 99% | 0.83 | 2.83 | 5.46 | 146.5 → 147.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.75 | 7.99 | 94.57 | 95% | 1.00 | 6.82 | 5.75 | 558.5 → 571.5 | 13 | 3.5 / 31 |

