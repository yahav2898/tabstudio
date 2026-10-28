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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.88 | 3.51 | 14.30 | 100% | 0.59 | 2.94 | 4.91 | 95.6 → 102.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.17 | 3.78 | 17.95 | 100% | 1.01 | 3.02 | 5.67 | 131.7 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.45 | 5.70 | 20.43 | 99% | 2.20 | 3.19 | 6.15 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.85 | 17.76 | 99% | 1.13 | 3.04 | 5.84 | 117.0 → 118.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 3.34 | 15.51 | 99% | 0.96 | 2.71 | 5.42 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.12 | 5.00 | 23.77 | 99% | 1.95 | 3.05 | 5.88 | 116.9 → 112.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.57 | 15.54 | 99% | 0.92 | 2.87 | 5.47 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 3.66 | 15.64 | 99% | 0.93 | 2.94 | 5.38 | 121.9 → 118.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 3.39 | 13.22 | 99% | 0.91 | 2.85 | 5.38 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 4.07 | 17.94 | 99% | 1.77 | 3.09 | 5.71 | 121.7 → 118.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.67 | 18.26 | 99% | 1.11 | 3.03 | 4.80 | 121.8 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.16 | 9.84 | 100% | 0.69 | 2.61 | 4.72 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| AttachingStyle - | 0.064 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 5.47 | 15.65 | 99% | 1.74 | 3.24 | 5.69 | 117.6 → 118.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.64 | 8.63 | 25.91 | 95% | 1.05 | 6.61 | 9.68 | 620.4 → 641.0 | 14 | 3.5 / 31 |

