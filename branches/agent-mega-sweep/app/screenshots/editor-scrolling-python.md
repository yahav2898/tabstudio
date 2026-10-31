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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.97 | 15.99 | 100% | 0.50 | 2.59 | 4.39 | 93.4 → 100.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.53 | 19.64 | 99% | 1.33 | 2.80 | 5.00 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.79 | 18.26 | 99% | 1.67 | 2.79 | 5.40 | 176.9 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 4.65 | 19.87 | 99% | 1.67 | 2.86 | 5.69 | 115.2 → 116.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.93 | 16.68 | 99% | 0.72 | 2.50 | 5.03 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.65 | 3.11 | 19.16 | 99% | 0.79 | 2.62 | 4.92 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.67 | 3.05 | 22.26 | 99% | 0.66 | 2.61 | 4.44 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.30 | 17.54 | 99% | 0.70 | 2.69 | 4.88 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 3.29 | 17.34 | 99% | 0.87 | 2.65 | 4.38 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.29 | 13.90 | 100% | 1.57 | 2.67 | 5.21 | 120.0 → 116.1 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.71 | 3.11 | 19.27 | 99% | 0.99 | 2.59 | 4.12 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.64 | 16.12 | 100% | 0.53 | 2.31 | 4.12 | 44.0 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.287 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 4.29 | 35.46 | 99% | 2.17 | 2.74 | 5.36 | 116.3 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.34 | 7.46 | 26.21 | 95% | 0.97 | 6.12 | 9.13 | 606.8 → 629.7 | 14 | 3.5 / 30 |

