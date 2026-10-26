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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.89 | 10.26 | 99% | 0.34 | 1.66 | 3.26 | 122.1 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 2.06 | 12.11 | 99% | 0.51 | 1.71 | 3.72 | 159.1 → 156.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 2.24 | 12.85 | 99% | 0.68 | 1.77 | 3.83 | 206.4 → 191.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.17 | 13.76 | 99% | 0.71 | 2.49 | 3.88 | 144.4 → 146.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 1.98 | 13.02 | 99% | 0.49 | 1.68 | 3.68 | 118.6 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.50 | 1.90 | 13.76 | 99% | 0.49 | 1.64 | 3.60 | 143.4 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.50 | 1.94 | 12.08 | 99% | 0.51 | 1.64 | 3.69 | 145.7 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 2.18 | 12.10 | 99% | 0.49 | 1.82 | 3.94 | 148.6 → 145.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.66 | 12.82 | 99% | 0.48 | 2.03 | 3.50 | 107.7 → 106.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 2.41 | 13.48 | 99% | 0.50 | 1.87 | 3.75 | 148.7 → 145.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.52 | 1.94 | 10.17 | 99% | 0.48 | 1.66 | 3.32 | 148.2 → 145.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.66 | 7.93 | 100% | 0.34 | 1.51 | 3.07 | 68.6 → 68.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.156 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
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
| this editor | 368 | 0.52 | 1.88 | 14.37 | 99% | 0.48 | 1.70 | 3.68 | 144.3 → 145.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.90 | 8.03 | 30.65 | 95% | 0.65 | 6.94 | 4.47 | 552.4 → 568.4 | 13 | 3.5 / 31 |

