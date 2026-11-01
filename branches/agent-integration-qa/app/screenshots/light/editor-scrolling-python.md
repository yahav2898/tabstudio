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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.38 | 19.30 | 100% | 0.59 | 2.83 | 4.99 | 93.3 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.86 | 20.32 | 100% | 1.60 | 2.99 | 5.51 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 4.55 | 18.63 | 98% | 1.96 | 2.98 | 5.80 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 3.64 | 30.20 | 99% | 0.82 | 2.97 | 4.98 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 3.32 | 16.82 | 99% | 0.88 | 2.73 | 5.18 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.72 | 3.34 | 13.62 | 99% | 0.81 | 2.79 | 5.29 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.82 | 4.14 | 20.58 | 99% | 1.82 | 2.92 | 5.26 | 117.0 → 113.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.31 | 18.36 | 99% | 0.76 | 2.79 | 4.72 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 3.21 | 13.08 | 99% | 0.58 | 2.74 | 4.83 | 80.3 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.84 | 3.54 | 15.74 | 100% | 0.80 | 2.96 | 4.97 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.83 | 3.54 | 19.68 | 99% | 1.69 | 2.97 | 4.79 | 119.7 → 115.7 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.91 | 7.73 | 100% | 0.57 | 2.54 | 4.26 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.303 |
| ArrangingLayoutable CachedLineNumberMargin | 0.057 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.09 | 37.96 | 99% | 0.66 | 2.65 | 4.58 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.52 | 11.01 | 35.14 | 92% | 2.63 | 8.24 | 10.25 | 598.7 → 627.3 | 15 | 3.5 / 30 |

