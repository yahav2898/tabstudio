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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 3.01 | 5.41 | 100% | 0.38 | 2.44 | 3.40 | 121.8 → 129.3 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.67 | 1.63 | 34.41 | 100% | 0.52 | 1.23 | 3.72 | 158.5 → 156.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.74 | 1.77 | 41.40 | 100% | 0.69 | 1.22 | 4.10 | 205.5 → 190.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 1.45 | 1.67 | 100% | 0.45 | 1.12 | 3.31 | 143.3 → 144.7 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 1.30 | 41.22 | 100% | 0.43 | 1.00 | 3.72 | 117.8 → 116.2 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 3.45 | 5.15 | 100% | 0.58 | 3.06 | 3.78 | 143.1 → 139.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 1.45 | 19.41 | 100% | 0.46 | 1.09 | 3.22 | 145.0 → 141.8 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 1.42 | 39.64 | 100% | 0.45 | 1.10 | 3.78 | 147.9 → 144.7 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.49 | 1.30 | 1.61 | 100% | 0.35 | 1.03 | 3.06 | 106.9 → 105.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 1.44 | 39.96 | 100% | 0.46 | 1.13 | 3.87 | 147.6 → 144.7 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 2.75 | 42.95 | 100% | 0.48 | 2.29 | 3.40 | 147.4 → 144.4 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.43 | 3.77 | 100% | 0.36 | 2.18 | 3.19 | 68.1 → 67.5 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.155 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 1.34 | 32.78 | 100% | 0.43 | 1.05 | 3.21 | 143.6 → 144.7 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.50 | 2.83 | 51.95 | 99% | 0.51 | 2.49 | 3.80 | 546.0 → 564.1 | 3 | 3.5 / 31 |

