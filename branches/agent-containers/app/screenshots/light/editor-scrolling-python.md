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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 2.98 | 12.71 | 99% | 0.51 | 2.53 | 4.77 | 132.6 → 141.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 3.37 | 17.50 | 99% | 0.70 | 2.73 | 4.87 | 170.5 → 167.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 3.41 | 17.58 | 99% | 0.83 | 2.64 | 5.01 | 216.8 → 201.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 3.33 | 14.61 | 99% | 0.66 | 2.72 | 4.85 | 155.6 → 157.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.02 | 3.48 | 12.43 | 99% | 0.91 | 2.71 | 4.94 | 130.0 → 129.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 3.13 | 18.95 | 99% | 0.70 | 2.58 | 4.63 | 155.9 → 149.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 3.16 | 15.67 | 99% | 0.65 | 2.61 | 4.62 | 156.5 → 153.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 3.43 | 16.21 | 99% | 0.71 | 2.69 | 5.02 | 159.0 → 156.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 2.83 | 11.09 | 99% | 0.50 | 2.40 | 4.22 | 117.4 → 117.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 3.81 | 15.42 | 99% | 0.71 | 2.79 | 4.68 | 158.5 → 156.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 3.35 | 76.91 | 99% | 0.52 | 2.64 | 4.13 | 159.5 → 154.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.52 | 10.23 | 100% | 0.42 | 2.20 | 3.38 | 79.5 → 78.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.239 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.78 | 14.93 | 99% | 0.51 | 2.38 | 4.25 | 153.5 → 155.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.33 | 7.16 | 29.28 | 95% | 0.69 | 6.63 | 5.44 | 552.3 → 584.0 | 14 | 3.5 / 31 |

