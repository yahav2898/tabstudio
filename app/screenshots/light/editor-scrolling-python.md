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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 4.86 | 12.26 | 99% | 0.50 | 3.26 | 5.58 | 134.9 → 141.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.81 | 16.17 | 99% | 0.52 | 2.38 | 4.86 | 169.2 → 167.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 2.93 | 16.38 | 99% | 0.69 | 2.38 | 4.90 | 216.1 → 201.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.72 | 13.76 | 99% | 0.51 | 2.37 | 4.80 | 155.2 → 155.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.52 | 12.46 | 99% | 0.49 | 2.16 | 4.63 | 129.3 → 126.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 4.86 | 21.57 | 99% | 1.52 | 4.34 | 5.25 | 155.4 → 152.1 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.61 | 2.68 | 15.70 | 99% | 0.52 | 2.34 | 4.81 | 155.8 → 153.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.75 | 16.31 | 99% | 0.51 | 2.36 | 4.79 | 159.4 → 155.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 2.58 | 11.63 | 99% | 0.41 | 2.29 | 4.62 | 118.9 → 116.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.74 | 15.29 | 99% | 0.51 | 2.34 | 4.74 | 158.8 → 155.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 4.83 | 18.55 | 99% | 0.72 | 2.78 | 4.83 | 158.9 → 157.5 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.47 | 11.61 | 99% | 0.38 | 2.18 | 3.83 | 79.8 → 80.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.218 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.73 | 17.89 | 99% | 0.49 | 2.36 | 4.74 | 154.8 → 156.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.80 | 5.04 | 23.32 | 96% | 0.61 | 4.55 | 5.49 | 558.1 → 574.9 | 13 | 3.5 / 31 |

