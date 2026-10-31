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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 3.23 | 18.27 | 99% | 0.48 | 2.76 | 4.38 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.35 | 19.25 | 99% | 0.87 | 2.76 | 5.01 | 129.3 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 4.31 | 19.33 | 99% | 2.26 | 2.81 | 5.38 | 176.1 → 162.1 | 5 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 3.97 | 13.69 | 99% | 1.40 | 2.69 | 5.05 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.87 | 17.53 | 99% | 0.68 | 2.39 | 4.84 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 3.07 | 18.53 | 99% | 0.79 | 2.56 | 4.35 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.69 | 3.09 | 16.45 | 99% | 0.69 | 2.58 | 4.59 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 5.03 | 21.44 | 99% | 2.40 | 3.32 | 5.70 | 119.8 → 115.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.58 | 2.89 | 13.61 | 99% | 0.50 | 2.55 | 3.97 | 80.3 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.70 | 3.13 | 19.06 | 99% | 0.71 | 2.67 | 4.49 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.68 | 3.13 | 16.79 | 99% | 0.68 | 2.63 | 4.55 | 119.7 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.53 | 17.92 | 100% | 0.44 | 2.25 | 4.04 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.360 |
| AttachingStyle - | 0.029 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable Track | 0.017 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Panel | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 3.35 | 34.68 | 99% | 0.69 | 2.79 | 4.73 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.43 | 8.79 | 27.03 | 94% | 1.64 | 7.38 | 9.48 | 601.6 → 624.2 | 14 | 3.5 / 30 |

