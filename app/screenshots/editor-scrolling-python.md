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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.93 | 15.20 | 99% | 0.41 | 2.64 | 4.21 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.19 | 17.07 | 99% | 0.93 | 2.69 | 4.64 | 131.7 → 129.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 5.18 | 19.85 | 99% | 2.38 | 3.15 | 5.62 | 178.7 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 3.08 | 15.13 | 99% | 0.72 | 2.65 | 5.07 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.85 | 17.23 | 99% | 0.80 | 2.45 | 4.97 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 3.01 | 18.44 | 99% | 0.72 | 2.53 | 4.84 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.45 | 17.17 | 99% | 1.35 | 2.80 | 5.03 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.24 | 25.04 | 99% | 1.31 | 2.69 | 5.30 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 2.79 | 15.20 | 99% | 0.47 | 2.49 | 4.73 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.95 | 17.87 | 99% | 0.64 | 2.57 | 5.00 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 3.28 | 20.78 | 99% | 1.24 | 2.67 | 4.62 | 121.7 → 118.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.93 | 10.76 | 100% | 1.38 | 2.48 | 4.32 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.246 |
| AttachingStyle - | 0.119 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.28 | 18.31 | 99% | 0.73 | 2.72 | 4.84 | 117.6 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.58 | 13.31 | 107.36 | 88% | 2.52 | 10.44 | 10.65 | 618.7 → 645.6 | 15 | 3.5 / 31 |

