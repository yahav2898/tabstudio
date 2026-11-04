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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 2.29 | 14.40 | 99% | 0.39 | 2.01 | 2.79 | 93.4 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.50 | 2.33 | 15.25 | 99% | 0.56 | 1.98 | 2.97 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.55 | 2.44 | 15.70 | 99% | 1.25 | 1.99 | 3.22 | 176.9 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.67 | 16.56 | 99% | 0.60 | 2.15 | 3.34 | 115.2 → 116.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.65 | 15.31 | 99% | 0.66 | 2.08 | 3.33 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.72 | 2.73 | 19.14 | 99% | 0.77 | 2.19 | 3.36 | 115.5 → 110.8 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.93 | 4.14 | 30.78 | 99% | 1.73 | 2.50 | 3.88 | 117.4 → 113.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.47 | 2.48 | 26.44 | 99% | 0.87 | 2.04 | 3.39 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.43 | 2.28 | 9.93 | 99% | 0.47 | 1.98 | 2.89 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.56 | 2.44 | 15.17 | 99% | 0.56 | 2.07 | 3.23 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 2.78 | 16.38 | 99% | 0.70 | 2.25 | 3.12 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.56 | 15.27 | 100% | 0.57 | 2.05 | 2.92 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.396 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 4.23 | 36.99 | 99% | 1.75 | 2.55 | 4.10 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.33 | 7.22 | 32.35 | 96% | 1.46 | 5.15 | 6.96 | 584.2 → 611.2 | 13 | 3.5 / 30 |

