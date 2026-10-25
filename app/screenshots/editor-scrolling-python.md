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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 1.84 | 10.79 | 99% | 0.37 | 1.51 | 4.03 | 121.0 → 128.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 3.21 | 15.24 | 99% | 0.67 | 2.58 | 4.80 | 157.7 → 155.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 2.20 | 14.06 | 99% | 0.83 | 1.59 | 4.61 | 204.7 → 189.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 1.95 | 13.57 | 99% | 0.56 | 1.53 | 4.36 | 142.4 → 143.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.07 | 10.61 | 99% | 0.56 | 1.59 | 4.40 | 116.7 → 115.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 2.87 | 12.43 | 99% | 0.63 | 1.93 | 4.65 | 142.4 → 138.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 1.97 | 14.16 | 99% | 0.56 | 1.57 | 4.61 | 144.3 → 140.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 1.97 | 13.96 | 99% | 0.55 | 1.56 | 4.45 | 147.1 → 143.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.96 | 65.55 | 99% | 0.57 | 2.49 | 4.49 | 106.1 → 104.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 1.99 | 13.15 | 99% | 0.58 | 1.55 | 4.38 | 146.8 → 143.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.02 | 13.50 | 99% | 0.57 | 1.57 | 4.02 | 146.6 → 143.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.50 | 9.80 | 99% | 0.37 | 1.25 | 3.67 | 67.0 → 66.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.317 |
| AttachingStyle - | 0.078 |
| ArrangingLayoutable NumberDrawing | 0.027 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 1.98 | 14.73 | 99% | 0.55 | 1.56 | 4.51 | 142.8 → 143.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.06 | 4.64 | 23.14 | 96% | 0.69 | 4.14 | 4.69 | 554.6 → 570.3 | 13 | 3.5 / 31 |

