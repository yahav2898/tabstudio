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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.31 | 1.13 | 7.76 | 100% | 0.23 | 0.97 | 2.30 | 120.1 → 127.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.40 | 1.26 | 10.30 | 99% | 0.36 | 1.02 | 2.67 | 156.5 → 154.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.46 | 1.41 | 10.48 | 99% | 0.51 | 1.05 | 2.74 | 203.2 → 188.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.35 | 1.21 | 10.04 | 99% | 0.34 | 1.01 | 2.56 | 141.7 → 143.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.35 | 2.61 | 9.76 | 100% | 0.48 | 1.05 | 2.68 | 116.7 → 115.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.45 | 2.61 | 8.28 | 100% | 0.48 | 2.28 | 2.79 | 142.0 → 137.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.35 | 1.21 | 10.80 | 99% | 0.36 | 1.01 | 2.62 | 143.3 → 140.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.21 | 10.42 | 99% | 0.35 | 1.01 | 2.71 | 146.4 → 143.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.30 | 1.15 | 6.10 | 100% | 0.26 | 0.96 | 2.49 | 105.7 → 104.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.36 | 1.21 | 10.22 | 99% | 0.35 | 1.00 | 2.66 | 145.9 → 143.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 1.15 | 10.17 | 99% | 0.34 | 1.00 | 2.42 | 146.0 → 142.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 2.18 | 7.96 | 100% | 0.33 | 0.92 | 2.26 | 67.7 → 67.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.255 |
| AttachingStyle - | 0.082 |
| FindingResource - | 0.018 |
| ArrangingLayoutable CachedLineNumberMargin | 0.017 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
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
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 1.27 | 9.86 | 99% | 0.36 | 1.03 | 2.68 | 142.3 → 143.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.11 | 2.81 | 16.94 | 96% | 0.40 | 2.42 | 2.83 | 546.3 → 564.5 | 12 | 3.5 / 31 |

