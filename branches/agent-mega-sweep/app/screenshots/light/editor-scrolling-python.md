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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 3.44 | 19.87 | 99% | 0.59 | 2.91 | 5.22 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 3.84 | 19.88 | 99% | 1.23 | 2.96 | 5.44 | 129.3 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 4.07 | 19.65 | 99% | 1.78 | 2.97 | 5.71 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.18 | 4.88 | 21.23 | 99% | 1.78 | 3.15 | 5.77 | 114.9 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 3.43 | 18.53 | 99% | 0.78 | 2.73 | 5.24 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.86 | 3.48 | 19.29 | 99% | 0.94 | 2.89 | 5.26 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.86 | 3.65 | 16.80 | 100% | 0.91 | 2.86 | 5.69 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.17 | 4.23 | 19.09 | 99% | 1.93 | 3.12 | 5.84 | 119.9 → 115.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.91 | 3.63 | 15.82 | 99% | 1.53 | 2.94 | 5.55 | 80.4 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.95 | 3.81 | 20.06 | 99% | 0.95 | 3.01 | 5.53 | 119.6 → 115.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.94 | 3.69 | 15.12 | 100% | 1.03 | 2.92 | 5.02 | 119.7 → 115.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.07 | 18.65 | 100% | 0.61 | 2.56 | 4.45 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.414 |
| AttachingStyle - | 0.087 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.016 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 3.71 | 37.72 | 99% | 0.88 | 3.00 | 5.44 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.59 | 7.81 | 33.26 | 95% | 1.08 | 6.80 | 9.85 | 599.5 → 625.1 | 14 | 3.5 / 30 |

