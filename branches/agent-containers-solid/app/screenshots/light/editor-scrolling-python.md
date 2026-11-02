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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.84 | 15.45 | 99% | 0.42 | 2.52 | 4.16 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.11 | 15.85 | 99% | 0.91 | 2.61 | 4.51 | 129.3 → 129.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 4.23 | 18.03 | 99% | 1.99 | 2.84 | 5.60 | 176.1 → 162.2 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.24 | 13.70 | 99% | 1.34 | 2.75 | 5.32 | 114.9 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.15 | 17.89 | 99% | 0.74 | 2.61 | 5.14 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.64 | 2.98 | 33.42 | 99% | 0.74 | 2.54 | 4.38 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.66 | 3.03 | 15.70 | 99% | 0.70 | 2.56 | 5.20 | 117.0 → 113.0 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 4.30 | 94.75 | 99% | 1.75 | 2.82 | 5.36 | 119.8 → 115.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 3.06 | 15.31 | 100% | 0.61 | 2.61 | 4.98 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.68 | 3.15 | 17.22 | 99% | 0.81 | 2.69 | 5.24 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.10 | 26.80 | 99% | 0.69 | 2.63 | 4.15 | 119.6 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.70 | 12.18 | 100% | 0.54 | 2.36 | 4.13 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.422 |
| ArrangingLayoutable LineDrawing | 0.080 |
| AttachingStyle - | 0.041 |
| ArrangingLayoutable ScrollContentPresenter | 0.023 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable Track | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.05 | 35.62 | 99% | 0.67 | 2.63 | 4.46 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.41 | 11.80 | 33.15 | 93% | 1.74 | 8.42 | 9.96 | 595.7 → 626.3 | 15 | 3.5 / 30 |

