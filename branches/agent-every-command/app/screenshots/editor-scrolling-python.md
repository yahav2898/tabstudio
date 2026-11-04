# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.11 | 16.54 | 99% | 0.46 | 2.71 | 4.52 | 94.2 → 101.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.67 | 24.69 | 99% | 1.90 | 2.94 | 5.49 | 131.0 → 131.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 3.94 | 16.32 | 99% | 1.80 | 2.88 | 5.72 | 178.8 → 164.7 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 3.22 | 14.10 | 99% | 0.65 | 2.71 | 4.72 | 116.3 → 117.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 3.02 | 14.72 | 99% | 0.70 | 2.56 | 5.11 | 92.2 → 89.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 4.99 | 78.29 | 98% | 1.50 | 2.94 | 5.33 | 116.6 → 111.9 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.75 | 3.39 | 16.86 | 99% | 1.46 | 2.76 | 5.49 | 118.6 → 114.5 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.36 | 16.62 | 99% | 0.83 | 2.83 | 5.39 | 121.4 → 117.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.69 | 3.27 | 14.48 | 99% | 0.71 | 2.73 | 5.19 | 81.2 → 78.8 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.75 | 3.42 | 14.69 | 99% | 1.46 | 2.76 | 5.09 | 121.2 → 117.3 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.66 | 3.09 | 13.72 | 100% | 0.83 | 2.63 | 4.67 | 121.2 → 117.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.58 | 10.46 | 100% | 0.45 | 2.28 | 4.03 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.31 | 36.93 | 99% | 0.72 | 2.74 | 5.23 | 117.5 → 118.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.36 | 8.79 | 37.29 | 94% | 1.75 | 7.38 | 9.18 | 609.1 → 635.1 | 15 | 3.5 / 30 |

