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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 1.58 | 12.04 | 99% | 0.36 | 1.33 | 3.58 | 121.7 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 1.81 | 14.31 | 99% | 0.54 | 1.38 | 3.86 | 158.7 → 156.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 1.92 | 13.84 | 99% | 0.71 | 1.39 | 3.98 | 205.7 → 190.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 3.83 | 10.91 | 100% | 0.61 | 3.43 | 4.12 | 144.3 → 145.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.45 | 12.75 | 99% | 0.48 | 1.14 | 3.73 | 117.9 → 116.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 1.67 | 13.74 | 99% | 0.51 | 1.30 | 3.81 | 143.1 → 139.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 1.58 | 11.82 | 99% | 0.48 | 1.23 | 3.71 | 145.0 → 141.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 2.10 | 10.78 | 99% | 0.57 | 1.48 | 3.95 | 148.0 → 145.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.96 | 65.45 | 99% | 0.49 | 2.41 | 3.79 | 107.8 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 1.64 | 13.34 | 99% | 0.47 | 1.28 | 3.72 | 147.7 → 144.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 1.71 | 11.70 | 99% | 0.49 | 1.34 | 3.44 | 147.8 → 144.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.28 | 10.53 | 100% | 0.33 | 1.05 | 3.19 | 68.1 → 67.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.154 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
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
| this editor | 368 | 0.66 | 2.89 | 19.21 | 99% | 0.60 | 2.46 | 3.91 | 144.1 → 145.7 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.63 | 3.55 | 28.83 | 96% | 0.57 | 3.06 | 4.08 | 548.7 → 563.2 | 13 | 3.5 / 31 |

