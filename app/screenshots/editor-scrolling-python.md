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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 1.91 | 41.45 | 100% | 0.37 | 1.65 | 3.87 | 93.4 → 100.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.66 | 2.19 | 3.13 | 100% | 0.74 | 1.69 | 4.58 | 129.7 → 130.4 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 2.34 | 45.96 | 100% | 1.08 | 1.74 | 4.79 | 176.8 → 162.7 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.08 | 42.91 | 100% | 0.58 | 1.71 | 4.67 | 115.1 → 116.1 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.64 | 4.23 | 100% | 1.23 | 1.67 | 4.77 | 91.5 → 89.2 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.63 | 2.99 | 5.14 | 100% | 2.15 | 2.29 | 4.75 | 115.5 → 110.8 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 2.17 | 3.17 | 100% | 0.58 | 1.71 | 4.86 | 117.4 → 113.3 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.60 | 2.11 | 3.11 | 100% | 0.59 | 1.69 | 4.66 | 120.2 → 116.1 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.51 | 1.95 | 3.85 | 100% | 0.43 | 1.61 | 4.02 | 80.7 → 78.3 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.60 | 2.09 | 42.41 | 100% | 0.59 | 1.67 | 4.11 | 119.9 → 116.1 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.59 | 2.24 | 4.22 | 100% | 1.19 | 1.75 | 3.42 | 120.0 → 116.0 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.44 | 4.80 | 100% | 1.09 | 1.50 | 3.89 | 44.0 → 43.1 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.241 |
| AttachingStyle - | 0.160 |
| ArrangingLayoutable CachedLineNumberMargin | 0.029 |
| MeasuringLayoutable Grid | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.06 | 3.23 | 100% | 0.58 | 1.69 | 3.97 | 116.3 → 117.4 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.92 | 4.71 | 71.66 | 99% | 0.69 | 3.98 | 7.55 | 579.5 → 603.9 | 2 | 3.5 / 30 |

