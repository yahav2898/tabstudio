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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.86 | 16.50 | 99% | 0.41 | 2.56 | 4.07 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 4.06 | 26.44 | 99% | 1.75 | 2.77 | 5.08 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.62 | 13.24 | 99% | 1.53 | 2.80 | 5.46 | 176.8 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.22 | 17.56 | 99% | 0.70 | 2.71 | 4.60 | 115.2 → 116.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.88 | 15.30 | 99% | 0.63 | 2.50 | 4.88 | 91.5 → 89.2 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 4.24 | 16.94 | 99% | 1.52 | 2.73 | 5.11 | 115.5 → 110.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.62 | 3.01 | 18.94 | 99% | 0.61 | 2.56 | 4.88 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.00 | 18.94 | 99% | 0.71 | 2.56 | 4.73 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.64 | 3.13 | 12.94 | 99% | 1.50 | 2.65 | 4.66 | 80.6 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.65 | 3.04 | 13.19 | 99% | 0.68 | 2.54 | 4.55 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 3.04 | 16.84 | 99% | 0.73 | 2.57 | 4.63 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.58 | 14.64 | 100% | 0.42 | 2.28 | 4.01 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.385 |
| ArrangingLayoutable TextView | 0.025 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable LineDrawing | 0.020 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.008 |
| MeasuringLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 3.03 | 37.15 | 99% | 0.62 | 2.58 | 4.39 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.28 | 12.46 | 36.17 | 92% | 2.52 | 7.76 | 8.83 | 602.2 → 635.1 | 15 | 3.5 / 30 |

