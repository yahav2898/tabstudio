# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 3.71 | 19.40 | 99% | 0.58 | 3.18 | 4.76 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.81 | 20.51 | 99% | 1.59 | 3.11 | 4.89 | 131.3 → 129.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 6.06 | 21.67 | 99% | 2.90 | 3.40 | 5.74 | 178.0 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.53 | 18.50 | 99% | 0.69 | 2.97 | 5.14 | 116.6 → 118.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 3.53 | 18.42 | 99% | 0.78 | 2.96 | 5.23 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 3.50 | 19.42 | 99% | 0.76 | 2.97 | 5.08 | 116.4 → 112.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 5.35 | 22.05 | 99% | 2.09 | 3.32 | 5.62 | 118.6 → 115.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.43 | 19.64 | 99% | 0.95 | 2.92 | 4.83 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 3.21 | 14.46 | 100% | 0.58 | 2.80 | 4.96 | 81.3 → 79.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 3.76 | 18.10 | 99% | 1.10 | 3.08 | 5.44 | 121.2 → 118.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.95 | 4.84 | 22.79 | 99% | 1.60 | 3.34 | 4.68 | 121.4 → 117.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 3.52 | 12.40 | 100% | 1.66 | 2.90 | 4.63 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.277 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
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
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.80 | 19.58 | 99% | 0.82 | 3.08 | 5.01 | 117.2 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.68 | 13.08 | 101.07 | 89% | 2.53 | 9.70 | 9.57 | 612.4 → 636.4 | 15 | 3.5 / 31 |

