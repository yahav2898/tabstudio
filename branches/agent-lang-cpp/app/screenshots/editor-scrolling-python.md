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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.27 | 1.55 | 100% | 0.31 | 0.98 | 3.00 | 121.7 → 129.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 1.37 | 2.13 | 100% | 0.47 | 1.02 | 3.16 | 158.5 → 156.3 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 1.53 | 41.86 | 100% | 0.64 | 1.09 | 3.74 | 205.8 → 190.8 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 1.36 | 38.12 | 100% | 0.44 | 1.06 | 3.50 | 143.5 → 145.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.38 | 3.38 | 100% | 0.44 | 0.96 | 3.53 | 117.9 → 116.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.68 | 21.81 | 100% | 0.52 | 2.26 | 3.69 | 143.6 → 139.3 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 1.40 | 35.94 | 100% | 0.45 | 1.07 | 3.55 | 145.3 → 142.1 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 1.36 | 1.66 | 100% | 0.45 | 1.04 | 3.30 | 148.2 → 144.9 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.29 | 35.37 | 100% | 0.33 | 1.01 | 3.30 | 106.9 → 105.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.51 | 1.37 | 1.70 | 100% | 0.44 | 1.04 | 3.58 | 147.9 → 145.0 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 1.51 | 36.83 | 100% | 0.45 | 1.07 | 3.38 | 147.8 → 144.6 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.46 | 3.24 | 100% | 0.33 | 2.18 | 3.03 | 68.2 → 68.0 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.197 |
| AttachingStyle - | 0.030 |
| MeasuringLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollBar | 0.010 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 1.39 | 33.22 | 100% | 0.44 | 1.04 | 3.25 | 144.0 → 145.0 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.53 | 2.59 | 44.98 | 99% | 0.50 | 2.21 | 3.68 | 547.6 → 570.1 | 2 | 3.5 / 31 |

