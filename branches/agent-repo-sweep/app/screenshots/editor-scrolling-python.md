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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.24 | 16.27 | 100% | 0.50 | 2.74 | 4.73 | 95.5 → 102.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 4.82 | 27.48 | 99% | 1.88 | 3.03 | 5.77 | 131.7 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 3.95 | 18.41 | 99% | 1.63 | 2.96 | 5.59 | 178.7 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.34 | 16.80 | 99% | 0.77 | 2.77 | 5.10 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.14 | 16.78 | 99% | 0.77 | 2.60 | 5.02 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 4.47 | 19.16 | 100% | 1.70 | 2.92 | 5.44 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 3.16 | 15.32 | 99% | 0.70 | 2.69 | 4.90 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.34 | 17.11 | 99% | 0.78 | 2.79 | 4.93 | 121.8 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 3.07 | 12.32 | 100% | 0.51 | 2.65 | 4.88 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.89 | 5.28 | 82.95 | 98% | 1.62 | 3.46 | 5.81 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 3.15 | 14.72 | 99% | 0.74 | 2.69 | 4.39 | 121.7 → 118.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.71 | 16.90 | 100% | 0.48 | 2.36 | 4.15 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.237 |
| ArrangingLayoutable ScrollBar | 0.040 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 4.50 | 18.66 | 99% | 1.27 | 2.90 | 5.02 | 117.6 → 118.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.21 | 6.57 | 24.58 | 96% | 0.85 | 5.86 | 8.65 | 620.0 → 642.2 | 14 | 3.5 / 31 |

