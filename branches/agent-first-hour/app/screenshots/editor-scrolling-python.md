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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.32 | 3.70 | 100% | 0.45 | 1.94 | 4.50 | 93.4 → 100.6 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.56 | 55.68 | 100% | 0.95 | 1.97 | 5.05 | 129.7 → 130.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.80 | 4.82 | 100% | 1.48 | 2.01 | 5.65 | 176.8 → 162.8 | 0 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.38 | 53.50 | 100% | 0.72 | 1.94 | 5.18 | 115.2 → 116.1 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.68 | 5.42 | 100% | 1.27 | 1.86 | 5.29 | 91.5 → 89.3 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.75 | 3.80 | 23.90 | 100% | 2.37 | 2.33 | 5.61 | 115.5 → 110.8 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.69 | 2.37 | 5.26 | 100% | 0.70 | 1.93 | 4.53 | 117.4 → 113.3 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.44 | 50.50 | 100% | 0.76 | 1.95 | 5.18 | 120.2 → 116.1 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 2.27 | 3.78 | 100% | 0.50 | 1.89 | 4.25 | 80.7 → 78.3 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.73 | 2.51 | 50.72 | 100% | 0.69 | 1.97 | 5.35 | 119.9 → 116.2 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.71 | 2.55 | 4.44 | 100% | 0.76 | 1.97 | 4.74 | 120.0 → 116.0 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.46 | 5.67 | 100% | 1.25 | 1.73 | 4.39 | 44.0 → 43.1 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.364 |
| AttachingStyle - | 0.050 |
| ArrangingLayoutable TextView | 0.012 |
| MeasuringLayoutable NumberDrawing | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.33 | 3.83 | 100% | 0.67 | 1.90 | 5.13 | 116.3 → 117.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.26 | 5.25 | 97.80 | 98% | 0.80 | 4.46 | 9.44 | 599.3 → 627.7 | 3 | 3.5 / 30 |

