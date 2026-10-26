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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 1.92 | 13.42 | 100% | 0.31 | 1.63 | 3.22 | 121.9 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 2.04 | 12.36 | 99% | 0.50 | 1.71 | 3.72 | 158.9 → 156.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 2.26 | 12.95 | 99% | 0.67 | 1.75 | 3.81 | 206.2 → 191.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 1.93 | 11.60 | 99% | 0.47 | 1.69 | 3.64 | 144.0 → 145.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.64 | 11.21 | 99% | 0.57 | 1.95 | 3.75 | 118.7 → 117.3 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 2.86 | 14.00 | 99% | 0.51 | 2.02 | 3.78 | 144.2 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 1.72 | 11.38 | 100% | 0.48 | 1.29 | 3.71 | 145.6 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.55 | 24.13 | 99% | 0.47 | 1.25 | 3.68 | 148.3 → 145.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.44 | 1.39 | 8.41 | 100% | 0.38 | 1.16 | 3.56 | 107.6 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 2.83 | 17.23 | 99% | 0.56 | 1.51 | 3.79 | 148.2 → 145.6 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 3.14 | 10.06 | 99% | 0.61 | 2.39 | 3.53 | 148.5 → 144.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.20 | 5.79 | 100% | 0.32 | 0.98 | 3.04 | 68.4 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.187 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| this editor | 368 | 0.52 | 1.48 | 13.57 | 100% | 0.48 | 1.20 | 3.70 | 144.1 → 145.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.55 | 3.71 | 19.72 | 96% | 0.57 | 3.23 | 3.88 | 548.9 → 570.4 | 13 | 3.5 / 31 |

