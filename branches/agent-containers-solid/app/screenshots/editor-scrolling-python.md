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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.02 | 18.00 | 100% | 0.47 | 2.70 | 4.63 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 3.42 | 16.80 | 99% | 0.89 | 2.70 | 5.14 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 3.79 | 17.58 | 99% | 1.62 | 2.74 | 5.31 | 176.9 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 3.56 | 17.77 | 99% | 1.70 | 2.85 | 4.94 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.54 | 16.27 | 99% | 1.46 | 2.74 | 5.62 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.90 | 3.57 | 34.29 | 99% | 0.97 | 2.86 | 5.93 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.94 | 3.79 | 22.06 | 99% | 1.07 | 2.93 | 6.15 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 3.26 | 17.28 | 99% | 1.13 | 2.66 | 5.12 | 120.2 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 3.44 | 14.78 | 99% | 1.69 | 2.72 | 5.32 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.71 | 3.19 | 15.04 | 99% | 0.75 | 2.63 | 5.15 | 120.0 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.07 | 17.74 | 99% | 0.67 | 2.60 | 4.17 | 120.0 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.83 | 10.82 | 100% | 0.66 | 2.44 | 4.23 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| AttachingStyle - | 0.136 |
| ArrangingLayoutable ScrollingTextLayer | 0.064 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.93 | 29.27 | 99% | 1.78 | 2.78 | 5.30 | 116.4 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.03 | 6.53 | 31.85 | 95% | 0.95 | 5.91 | 8.82 | 603.8 → 631.0 | 14 | 3.5 / 30 |

