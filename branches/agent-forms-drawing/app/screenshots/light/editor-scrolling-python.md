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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.34 | 11.68 | 99% | 0.46 | 1.98 | 4.36 | 128.6 → 136.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 3.22 | 16.39 | 99% | 0.71 | 2.67 | 4.99 | 165.0 → 163.2 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.24 | 5.20 | 19.33 | 99% | 1.21 | 3.17 | 5.92 | 213.3 → 197.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.75 | 16.40 | 99% | 0.64 | 2.40 | 4.87 | 150.4 → 151.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.31 | 13.75 | 99% | 0.73 | 1.78 | 5.63 | 125.4 → 123.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.32 | 11.72 | 100% | 0.64 | 1.82 | 4.95 | 149.5 → 145.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 4.41 | 12.66 | 99% | 0.79 | 2.70 | 5.49 | 152.3 → 149.5 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.50 | 15.48 | 99% | 0.69 | 1.99 | 5.04 | 155.1 → 151.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.23 | 11.70 | 99% | 0.49 | 1.79 | 4.70 | 114.6 → 112.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.39 | 13.39 | 99% | 0.65 | 1.88 | 4.64 | 154.5 → 151.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 3.47 | 80.31 | 99% | 0.89 | 2.65 | 4.88 | 154.7 → 151.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.20 | 11.64 | 100% | 0.63 | 1.79 | 4.94 | 76.4 → 74.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.238 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 2.59 | 17.23 | 99% | 0.69 | 2.00 | 5.04 | 150.6 → 151.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.31 | 10.22 | 27.80 | 95% | 2.36 | 7.26 | 5.90 | 552.3 → 577.9 | 13 | 3.5 / 31 |

