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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.80 | 9.30 | 99% | 0.31 | 1.60 | 2.70 | 121.7 → 129.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.24 | 14.36 | 99% | 0.53 | 1.79 | 3.33 | 158.5 → 157.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.13 | 2.80 | 15.80 | 99% | 0.92 | 2.05 | 3.81 | 207.0 → 192.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.93 | 3.07 | 14.80 | 99% | 0.71 | 2.25 | 3.68 | 144.8 → 145.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 2.72 | 63.61 | 99% | 0.57 | 2.05 | 3.44 | 119.1 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.05 | 15.27 | 99% | 0.56 | 1.62 | 3.37 | 143.5 → 139.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.13 | 19.36 | 99% | 0.61 | 1.58 | 3.57 | 145.9 → 142.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.12 | 11.82 | 99% | 0.63 | 1.57 | 3.77 | 148.7 → 144.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.48 | 8.82 | 100% | 0.42 | 1.22 | 3.19 | 106.9 → 105.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 3.56 | 13.00 | 99% | 0.67 | 2.55 | 3.37 | 148.3 → 145.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.47 | 1.50 | 13.94 | 99% | 0.45 | 1.21 | 2.65 | 147.2 → 144.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.27 | 8.97 | 100% | 0.38 | 1.04 | 2.48 | 68.1 → 67.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.166 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 1.65 | 14.36 | 99% | 0.50 | 1.31 | 3.30 | 144.0 → 145.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.01 | 6.92 | 27.29 | 95% | 0.75 | 6.22 | 4.37 | 550.3 → 573.7 | 14 | 3.5 / 31 |

