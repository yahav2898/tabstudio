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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 1.99 | 18.28 | 99% | 0.45 | 1.64 | 4.93 | 122.2 → 129.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.89 | 18.52 | 99% | 0.80 | 2.51 | 5.57 | 158.7 → 156.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 2.76 | 20.65 | 99% | 0.93 | 1.96 | 5.79 | 205.8 → 190.7 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.17 | 18.29 | 99% | 0.61 | 1.71 | 5.40 | 143.8 → 145.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.15 | 18.40 | 99% | 0.62 | 1.68 | 5.35 | 118.8 → 117.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 2.87 | 94.35 | 99% | 0.72 | 2.02 | 5.74 | 143.8 → 139.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.27 | 17.34 | 99% | 0.62 | 1.78 | 5.38 | 145.6 → 142.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.26 | 19.95 | 99% | 0.67 | 1.82 | 5.34 | 148.6 → 145.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.00 | 70.81 | 99% | 0.61 | 2.44 | 5.20 | 108.1 → 106.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.13 | 17.10 | 99% | 0.62 | 1.70 | 5.27 | 148.2 → 145.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.07 | 18.57 | 99% | 0.62 | 1.64 | 4.50 | 148.0 → 144.6 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.81 | 10.20 | 100% | 0.47 | 1.46 | 4.35 | 69.3 → 69.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.337 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 2.33 | 16.94 | 100% | 0.65 | 1.80 | 5.41 | 144.3 → 145.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.31 | 4.63 | 28.12 | 97% | 0.71 | 4.07 | 5.59 | 548.8 → 566.5 | 8 | 3.5 / 31 |

