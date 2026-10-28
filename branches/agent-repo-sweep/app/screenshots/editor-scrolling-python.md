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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.17 | 15.41 | 99% | 0.50 | 2.74 | 4.80 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.58 | 15.72 | 99% | 1.27 | 2.85 | 5.26 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.37 | 5.97 | 21.48 | 98% | 2.84 | 3.31 | 6.64 | 178.7 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.12 | 3.72 | 21.70 | 99% | 0.88 | 3.09 | 5.38 | 117.0 → 118.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.86 | 3.44 | 12.65 | 100% | 0.92 | 2.83 | 5.61 | 92.3 → 90.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 3.41 | 18.54 | 99% | 0.77 | 2.89 | 5.64 | 116.9 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 4.23 | 21.47 | 99% | 1.75 | 3.00 | 6.02 | 119.0 → 115.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.22 | 12.23 | 100% | 0.69 | 2.71 | 5.23 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 3.20 | 19.36 | 99% | 0.65 | 2.73 | 4.92 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 3.64 | 19.41 | 99% | 0.80 | 2.96 | 5.58 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 5.10 | 92.34 | 99% | 1.82 | 3.04 | 5.37 | 121.7 → 118.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.78 | 11.07 | 100% | 1.33 | 2.42 | 4.26 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.357 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 3.90 | 16.44 | 99% | 0.90 | 3.11 | 5.76 | 117.6 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.16 | 13.57 | 44.80 | 84% | 2.74 | 9.93 | 11.74 | 622.4 → 652.5 | 15 | 3.5 / 31 |

