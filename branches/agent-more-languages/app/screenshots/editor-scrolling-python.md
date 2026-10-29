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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.97 | 3.57 | 15.13 | 99% | 0.66 | 3.01 | 5.25 | 95.6 → 102.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 4.76 | 20.53 | 99% | 1.68 | 3.07 | 5.72 | 131.8 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.28 | 4.10 | 20.77 | 99% | 1.78 | 3.02 | 6.01 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 3.75 | 16.49 | 99% | 0.90 | 3.04 | 5.63 | 117.0 → 118.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.69 | 15.81 | 99% | 0.93 | 2.91 | 5.70 | 92.4 → 90.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.97 | 3.98 | 20.34 | 100% | 1.73 | 2.97 | 5.75 | 116.9 → 112.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 3.68 | 15.91 | 99% | 0.93 | 2.99 | 5.70 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.98 | 3.56 | 18.32 | 99% | 0.86 | 2.96 | 5.53 | 121.9 → 118.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.88 | 3.62 | 17.21 | 99% | 1.44 | 3.00 | 5.86 | 81.7 → 79.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 4.29 | 16.94 | 99% | 1.72 | 3.07 | 6.00 | 121.7 → 118.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 3.60 | 18.47 | 99% | 1.07 | 2.94 | 4.83 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.11 | 14.54 | 100% | 0.64 | 2.64 | 4.60 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.17 | 3.97 | 94.42 | 99% | 2.02 | 3.13 | 6.09 | 117.7 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.72 | 8.25 | 30.68 | 95% | 1.03 | 6.95 | 10.28 | 619.3 → 641.1 | 14 | 3.5 / 31 |

