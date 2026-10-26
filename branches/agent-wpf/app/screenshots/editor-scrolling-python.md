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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.06 | 11.27 | 99% | 0.40 | 1.73 | 2.88 | 121.9 → 129.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 2.21 | 13.01 | 99% | 0.56 | 1.81 | 3.06 | 158.7 → 156.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 2.47 | 13.54 | 99% | 0.77 | 1.88 | 3.44 | 206.3 → 191.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.66 | 15.81 | 99% | 0.77 | 2.48 | 3.53 | 144.3 → 145.7 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.07 | 10.32 | 100% | 0.49 | 1.68 | 3.07 | 118.3 → 116.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.04 | 14.03 | 99% | 0.50 | 1.71 | 3.09 | 143.4 → 139.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 2.12 | 13.50 | 99% | 0.53 | 1.74 | 3.17 | 145.5 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.15 | 13.68 | 99% | 0.51 | 1.74 | 3.16 | 148.3 → 145.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.21 | 10.93 | 99% | 0.48 | 1.83 | 3.31 | 107.5 → 106.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 3.32 | 14.78 | 99% | 0.67 | 2.48 | 3.42 | 148.8 → 145.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.15 | 17.83 | 99% | 0.53 | 1.78 | 2.76 | 148.2 → 144.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.76 | 11.44 | 100% | 0.42 | 1.51 | 2.57 | 68.6 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.200 |
| AttachingStyle - | 0.047 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.12 | 14.41 | 99% | 0.51 | 1.77 | 3.06 | 144.1 → 145.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.66 | 7.30 | 75.14 | 96% | 2.43 | 5.67 | 3.80 | 549.4 → 576.4 | 13 | 3.5 / 31 |

