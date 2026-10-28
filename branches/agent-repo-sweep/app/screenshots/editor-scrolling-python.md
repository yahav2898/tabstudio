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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 4.25 | 15.33 | 99% | 1.78 | 2.56 | 5.67 | 95.6 → 102.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.64 | 2.79 | 14.56 | 99% | 0.89 | 2.30 | 5.32 | 131.7 → 129.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.06 | 14.31 | 99% | 1.59 | 2.31 | 5.76 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.77 | 14.08 | 99% | 0.60 | 2.35 | 5.58 | 117.0 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 4.00 | 14.73 | 99% | 1.93 | 2.47 | 6.72 | 92.3 → 90.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.72 | 12.02 | 99% | 0.56 | 2.24 | 5.40 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 2.64 | 14.05 | 99% | 0.57 | 2.25 | 5.41 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 3.09 | 15.72 | 99% | 0.76 | 2.49 | 5.86 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.12 | 17.86 | 99% | 1.70 | 2.49 | 6.01 | 81.7 → 79.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 3.01 | 13.11 | 99% | 0.67 | 2.41 | 5.78 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.81 | 15.63 | 99% | 0.68 | 2.36 | 5.19 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.47 | 8.44 | 100% | 0.42 | 2.08 | 4.96 | 43.7 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.267 |
| ArrangingLayoutable TextView | 0.029 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable Grid | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.37 | 14.71 | 99% | 2.03 | 2.66 | 6.36 | 117.7 → 118.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.01 | 7.47 | 27.59 | 95% | 0.99 | 5.90 | 11.77 | 621.1 → 641.1 | 15 | 3.5 / 31 |

