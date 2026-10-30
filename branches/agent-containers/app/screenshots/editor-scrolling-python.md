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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.26 | 3.52 | 100% | 0.47 | 1.92 | 4.52 | 95.5 → 102.5 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 2.44 | 55.20 | 100% | 0.98 | 1.94 | 5.13 | 131.9 → 129.5 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.69 | 4.26 | 100% | 1.36 | 1.97 | 5.43 | 178.6 → 164.0 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.14 | 52.00 | 100% | 2.20 | 2.31 | 5.06 | 117.0 → 118.3 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.33 | 3.66 | 100% | 0.74 | 1.86 | 5.14 | 92.3 → 90.3 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.45 | 49.10 | 100% | 0.68 | 1.89 | 5.00 | 116.8 → 112.7 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.31 | 4.67 | 100% | 0.61 | 1.88 | 4.73 | 119.0 → 115.4 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.31 | 47.94 | 100% | 0.63 | 1.87 | 4.95 | 121.8 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 3.04 | 6.13 | 100% | 1.39 | 2.36 | 4.68 | 81.7 → 79.7 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.26 | 30.48 | 100% | 0.64 | 1.88 | 4.72 | 121.6 → 118.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.27 | 3.19 | 100% | 0.63 | 1.84 | 4.39 | 121.7 → 118.1 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.89 | 39.99 | 100% | 0.42 | 1.58 | 3.96 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.215 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.70 | 42.78 | 100% | 0.96 | 1.93 | 5.25 | 117.6 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.37 | 7.33 | 67.68 | 97% | 1.41 | 5.73 | 9.19 | 613.4 → 629.1 | 3 | 3.5 / 31 |

