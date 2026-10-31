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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.11 | 14.73 | 99% | 0.49 | 2.72 | 4.52 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.39 | 17.27 | 99% | 1.30 | 2.78 | 5.31 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 5.78 | 95.70 | 98% | 2.89 | 3.03 | 5.96 | 178.8 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.34 | 17.23 | 99% | 0.83 | 2.77 | 5.10 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.16 | 16.51 | 99% | 0.74 | 2.57 | 5.26 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 3.48 | 18.00 | 99% | 0.84 | 2.75 | 5.25 | 116.9 → 112.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 4.64 | 18.17 | 99% | 1.84 | 3.15 | 6.43 | 119.1 → 115.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 3.43 | 16.36 | 99% | 0.82 | 2.87 | 5.11 | 121.9 → 118.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.33 | 13.13 | 99% | 0.60 | 2.81 | 5.16 | 81.7 → 79.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 4.10 | 18.46 | 99% | 1.57 | 2.88 | 5.28 | 121.6 → 118.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 3.69 | 16.09 | 99% | 1.49 | 2.89 | 4.97 | 121.8 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.83 | 12.02 | 100% | 0.51 | 2.45 | 4.38 | 43.7 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.91 | 3.49 | 15.87 | 99% | 0.81 | 2.88 | 5.57 | 117.6 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.52 | 10.27 | 34.83 | 93% | 1.73 | 8.95 | 9.96 | 623.4 → 641.3 | 14 | 3.5 / 31 |

