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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.36 | 17.07 | 100% | 0.52 | 2.90 | 4.98 | 95.5 → 102.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.45 | 15.81 | 99% | 1.46 | 2.82 | 5.15 | 131.8 → 129.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.82 | 18.67 | 99% | 1.59 | 2.89 | 5.33 | 178.7 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.27 | 16.77 | 99% | 0.74 | 2.78 | 4.89 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 3.35 | 15.76 | 99% | 1.55 | 2.64 | 5.02 | 92.3 → 90.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 4.31 | 17.45 | 99% | 1.50 | 2.79 | 5.14 | 116.9 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 3.19 | 16.56 | 99% | 0.71 | 2.67 | 4.90 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 3.25 | 16.61 | 99% | 0.71 | 2.69 | 5.11 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 3.02 | 12.93 | 99% | 0.50 | 2.61 | 4.82 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 4.23 | 20.02 | 99% | 1.71 | 3.06 | 6.35 | 121.7 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.25 | 17.04 | 99% | 0.69 | 2.77 | 4.32 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.75 | 8.44 | 100% | 0.47 | 2.43 | 4.05 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.240 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.50 | 19.30 | 99% | 1.51 | 2.89 | 5.09 | 117.6 → 118.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.19 | 7.24 | 38.22 | 95% | 1.53 | 6.06 | 8.94 | 618.3 → 641.1 | 14 | 3.5 / 31 |

