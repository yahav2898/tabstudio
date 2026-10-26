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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 1.43 | 1.78 | 100% | 0.36 | 1.18 | 3.39 | 121.9 → 129.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 1.65 | 44.47 | 100% | 0.54 | 1.23 | 4.19 | 159.0 → 156.6 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 1.86 | 39.18 | 100% | 0.73 | 1.23 | 4.22 | 206.2 → 191.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.98 | 10.29 | 100% | 0.66 | 2.45 | 3.98 | 144.1 → 145.7 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.44 | 1.71 | 100% | 0.49 | 1.10 | 3.93 | 118.2 → 116.5 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 1.70 | 36.35 | 100% | 0.51 | 1.30 | 3.95 | 143.4 → 139.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.77 | 41.68 | 100% | 0.52 | 1.48 | 4.05 | 145.6 → 142.3 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 1.75 | 2.08 | 100% | 0.51 | 1.48 | 4.02 | 148.5 → 145.3 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.72 | 61.45 | 99% | 0.44 | 2.34 | 3.98 | 107.7 → 106.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 1.71 | 2.03 | 100% | 0.52 | 1.41 | 3.61 | 148.3 → 145.3 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 1.80 | 37.44 | 100% | 0.53 | 1.48 | 3.63 | 148.3 → 144.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.50 | 1.83 | 100% | 0.38 | 1.29 | 3.39 | 68.6 → 68.1 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.176 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 2.21 | 38.58 | 100% | 0.56 | 1.62 | 4.28 | 144.3 → 145.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.03 | 6.94 | 54.90 | 97% | 0.75 | 5.54 | 4.38 | 555.1 → 572.7 | 2 | 3.5 / 31 |

