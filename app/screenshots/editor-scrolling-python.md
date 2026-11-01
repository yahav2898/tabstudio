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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 3.03 | 14.45 | 99% | 0.44 | 2.66 | 4.68 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.31 | 18.29 | 99% | 1.41 | 2.77 | 4.97 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 3.60 | 18.17 | 99% | 1.54 | 2.78 | 5.30 | 176.9 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 4.64 | 18.39 | 100% | 1.71 | 2.90 | 5.40 | 115.2 → 116.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.40 | 16.53 | 99% | 1.34 | 2.70 | 5.31 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 3.21 | 19.59 | 99% | 0.70 | 2.73 | 4.73 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.65 | 3.03 | 28.10 | 99% | 0.64 | 2.58 | 4.62 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.65 | 3.19 | 17.47 | 99% | 0.72 | 2.65 | 4.82 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 4.02 | 12.87 | 100% | 1.68 | 2.80 | 5.27 | 80.7 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.48 | 20.70 | 99% | 0.73 | 2.84 | 5.29 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 3.38 | 19.06 | 99% | 0.75 | 2.81 | 5.07 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.83 | 13.82 | 100% | 0.51 | 2.46 | 4.13 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.256 |
| AttachingStyle - | 0.097 |
| RaisingRoutedEvent ScrollBar | 0.058 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.13 | 40.19 | 100% | 0.65 | 2.68 | 4.43 | 116.3 → 117.4 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.72 | 7.02 | 29.53 | 95% | 0.93 | 6.17 | 9.03 | 602.3 → 630.0 | 14 | 3.5 / 30 |

