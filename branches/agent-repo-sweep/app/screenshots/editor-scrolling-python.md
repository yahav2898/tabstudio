# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 3.90 | 11.66 | 100% | 0.70 | 3.23 | 5.63 | 95.6 → 102.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.74 | 22.11 | 99% | 1.03 | 3.04 | 5.82 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 3.96 | 20.58 | 99% | 1.75 | 3.05 | 5.95 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 3.68 | 22.51 | 99% | 0.94 | 2.97 | 5.78 | 117.0 → 118.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.84 | 3.62 | 18.81 | 99% | 1.78 | 2.86 | 5.69 | 92.4 → 90.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 3.58 | 20.49 | 99% | 1.08 | 2.96 | 5.93 | 116.9 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 3.69 | 19.50 | 99% | 0.88 | 3.02 | 5.52 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.14 | 3.78 | 18.88 | 99% | 1.67 | 2.98 | 5.85 | 121.9 → 118.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 3.59 | 14.47 | 99% | 0.69 | 2.95 | 5.54 | 81.8 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 3.65 | 20.31 | 99% | 0.88 | 2.95 | 5.68 | 121.6 → 118.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 3.54 | 18.81 | 99% | 0.80 | 2.93 | 4.72 | 121.7 → 118.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 3.04 | 17.72 | 100% | 0.60 | 2.58 | 4.63 | 43.7 → 42.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.444 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable CachedLineNumberMargin | 0.034 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| ArrangingLayoutable NumberDrawing | 0.018 |
| FindingResource - | 0.014 |
| MeasuringLayoutable Grid | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
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
| this editor | 368 | 0.93 | 3.51 | 17.57 | 99% | 0.87 | 2.92 | 5.63 | 117.6 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.57 | 7.76 | 28.45 | 95% | 0.96 | 6.63 | 9.74 | 621.3 → 643.3 | 15 | 3.5 / 31 |

