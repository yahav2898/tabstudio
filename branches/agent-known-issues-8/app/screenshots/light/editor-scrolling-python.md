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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.09 | 14.36 | 99% | 0.49 | 2.68 | 4.44 | 94.0 → 101.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.49 | 16.66 | 99% | 1.02 | 2.77 | 5.27 | 130.6 → 131.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 3.96 | 18.59 | 99% | 1.80 | 2.91 | 5.59 | 178.1 → 163.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 4.39 | 19.22 | 99% | 2.42 | 2.95 | 5.67 | 116.0 → 117.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.02 | 13.55 | 99% | 0.78 | 2.58 | 5.08 | 91.8 → 89.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.76 | 3.34 | 23.39 | 99% | 0.82 | 2.71 | 4.92 | 116.2 → 111.6 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.78 | 3.31 | 17.86 | 99% | 0.72 | 2.70 | 4.84 | 118.2 → 114.1 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.77 | 3.36 | 18.13 | 99% | 0.79 | 2.73 | 5.10 | 121.0 → 116.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.74 | 3.75 | 15.03 | 100% | 1.55 | 2.73 | 5.14 | 80.8 → 78.5 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.33 | 16.47 | 99% | 0.82 | 2.69 | 5.21 | 120.7 → 116.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.76 | 3.23 | 15.74 | 99% | 0.71 | 2.66 | 4.70 | 120.8 → 116.8 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.82 | 15.70 | 100% | 0.56 | 2.40 | 4.10 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.249 |
| AttachingStyle - | 0.066 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.35 | 35.23 | 99% | 0.72 | 2.75 | 4.64 | 117.1 → 118.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.22 | 7.35 | 26.71 | 96% | 0.88 | 6.22 | 9.11 | 602.3 → 630.5 | 14 | 3.5 / 30 |

