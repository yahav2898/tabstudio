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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.74 | 11.46 | 99% | 0.39 | 2.45 | 4.95 | 134.1 → 143.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.88 | 16.40 | 99% | 0.56 | 2.46 | 5.00 | 171.4 → 169.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.18 | 15.96 | 99% | 0.71 | 2.57 | 5.15 | 220.3 → 203.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.91 | 15.43 | 99% | 0.55 | 2.49 | 5.27 | 156.4 → 159.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 4.86 | 19.45 | 99% | 0.85 | 4.37 | 5.50 | 131.3 → 130.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.93 | 16.17 | 99% | 0.55 | 2.43 | 4.93 | 156.9 → 151.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.90 | 15.02 | 99% | 0.56 | 2.51 | 4.99 | 158.3 → 155.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.98 | 15.27 | 99% | 0.55 | 2.49 | 5.05 | 160.9 → 158.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 2.69 | 10.55 | 99% | 0.42 | 2.37 | 4.57 | 119.2 → 117.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 4.62 | 88.55 | 99% | 0.84 | 2.76 | 5.44 | 162.2 → 158.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.94 | 14.87 | 99% | 0.55 | 2.52 | 4.56 | 160.6 → 157.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.41 | 10.59 | 99% | 0.41 | 2.15 | 3.92 | 79.6 → 80.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.83 | 16.97 | 99% | 0.54 | 2.47 | 4.98 | 156.7 → 158.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.46 | 7.60 | 34.63 | 96% | 1.24 | 6.25 | 6.19 | 562.7 → 591.9 | 14 | 3.5 / 31 |

