# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.99 | 2.80 | 14.58 | 99% | 0.59 | 2.31 | 5.32 | 127.5 → 134.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.33 | 4.50 | 17.37 | 99% | 2.48 | 2.70 | 5.88 | 164.4 → 162.5 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 3.38 | 19.39 | 99% | 1.15 | 2.42 | 5.93 | 211.3 → 196.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.15 | 3.51 | 14.63 | 99% | 0.83 | 2.86 | 5.52 | 149.8 → 151.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.97 | 3.38 | 15.10 | 99% | 0.79 | 2.77 | 5.60 | 124.2 → 122.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.21 | 4.01 | 17.50 | 99% | 0.91 | 3.05 | 5.62 | 149.8 → 145.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.14 | 3.51 | 15.90 | 99% | 0.81 | 2.91 | 5.51 | 151.8 → 148.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.17 | 3.52 | 18.74 | 99% | 0.79 | 2.91 | 5.33 | 154.6 → 151.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 3.52 | 13.90 | 99% | 0.70 | 2.82 | 5.33 | 113.3 → 112.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 3.68 | 16.92 | 99% | 0.86 | 3.05 | 5.59 | 154.6 → 151.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 3.56 | 16.57 | 99% | 0.78 | 2.95 | 4.87 | 153.6 → 149.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.47 | 9.29 | 100% | 0.60 | 2.01 | 4.46 | 74.7 → 74.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.380 |
| AttachingStyle - | 0.118 |
| ArrangingLayoutable ScrollingTextLayer | 0.031 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 3.73 | 15.24 | 99% | 0.84 | 3.04 | 5.67 | 150.4 → 151.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.67 | 6.44 | 39.96 | 96% | 0.94 | 5.48 | 5.70 | 552.1 → 576.4 | 13 | 3.5 / 31 |

