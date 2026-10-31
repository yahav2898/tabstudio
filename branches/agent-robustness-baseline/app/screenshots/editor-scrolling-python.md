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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.23 | 16.82 | 99% | 0.55 | 2.78 | 4.83 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 4.26 | 85.68 | 99% | 1.68 | 3.00 | 5.61 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 4.38 | 18.71 | 98% | 1.83 | 2.93 | 5.94 | 178.7 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 3.43 | 15.74 | 99% | 0.73 | 2.73 | 5.17 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 3.09 | 16.61 | 99% | 0.73 | 2.59 | 5.05 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 4.33 | 17.22 | 99% | 1.63 | 2.94 | 5.58 | 116.8 → 112.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 3.47 | 18.36 | 99% | 0.84 | 2.79 | 5.34 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.38 | 16.31 | 99% | 0.78 | 2.81 | 5.10 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 3.09 | 13.66 | 99% | 0.59 | 2.64 | 4.83 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 4.60 | 18.63 | 99% | 2.45 | 3.03 | 5.93 | 121.6 → 118.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 3.21 | 15.57 | 100% | 0.77 | 2.70 | 4.39 | 121.7 → 118.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.77 | 15.35 | 100% | 0.50 | 2.40 | 4.11 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.243 |
| ArrangingLayoutable CachedLineNumberMargin | 0.059 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
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
| this editor | 368 | 0.73 | 3.56 | 19.93 | 99% | 0.93 | 2.86 | 5.21 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.66 | 9.66 | 27.80 | 94% | 1.77 | 7.52 | 9.90 | 623.5 → 639.9 | 15 | 3.5 / 31 |

