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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.57 | 13.74 | 99% | 0.57 | 2.10 | 5.20 | 127.1 → 134.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.30 | 4.93 | 76.85 | 99% | 0.91 | 3.17 | 5.81 | 163.9 → 162.4 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.32 | 3.71 | 18.05 | 99% | 1.09 | 2.94 | 5.89 | 211.1 → 196.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.07 | 3.40 | 17.04 | 99% | 0.77 | 2.84 | 5.59 | 149.8 → 151.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.89 | 3.12 | 14.54 | 99% | 0.76 | 2.63 | 5.52 | 123.9 → 122.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 4.23 | 14.45 | 99% | 0.91 | 2.98 | 5.76 | 149.8 → 144.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.03 | 3.41 | 17.45 | 99% | 0.78 | 2.79 | 6.01 | 151.3 → 147.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.04 | 3.39 | 17.39 | 99% | 0.76 | 2.77 | 5.60 | 153.9 → 150.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.96 | 3.72 | 75.98 | 99% | 0.69 | 2.95 | 5.59 | 113.8 → 111.9 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 3.20 | 15.54 | 99% | 0.71 | 2.67 | 5.40 | 153.2 → 150.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 3.36 | 14.28 | 99% | 0.73 | 2.78 | 4.80 | 153.3 → 150.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.67 | 3.00 | 10.35 | 100% | 0.57 | 2.51 | 4.25 | 74.7 → 74.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.451 |
| ArrangingLayoutable TextView | 0.104 |
| AttachingStyle - | 0.091 |
| ArrangingLayoutable NumberDrawing | 0.060 |
| ArrangingLayoutable Track | 0.030 |
| ArrangingLayoutable LineDrawing | 0.023 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| FindingResource - | 0.015 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.09 | 3.48 | 16.47 | 99% | 0.78 | 2.82 | 5.46 | 149.9 → 151.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.64 | 7.18 | 26.27 | 96% | 0.93 | 5.27 | 6.04 | 554.1 → 575.1 | 13 | 3.5 / 31 |

