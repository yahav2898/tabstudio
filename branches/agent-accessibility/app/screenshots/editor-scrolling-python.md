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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.83 | 17.77 | 99% | 0.42 | 2.57 | 4.30 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 3.11 | 18.82 | 99% | 0.85 | 2.59 | 4.58 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 3.39 | 18.69 | 99% | 1.43 | 2.63 | 5.18 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 3.12 | 18.14 | 99% | 1.29 | 2.64 | 4.84 | 115.2 → 116.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 4.37 | 15.69 | 100% | 1.69 | 2.68 | 5.19 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 3.05 | 19.57 | 99% | 0.71 | 2.56 | 4.55 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.65 | 2.98 | 21.87 | 99% | 0.71 | 2.58 | 4.75 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.11 | 17.55 | 100% | 0.72 | 2.64 | 5.09 | 120.2 → 116.1 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.53 | 2.86 | 12.15 | 100% | 0.45 | 2.51 | 4.78 | 80.6 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.89 | 5.02 | 20.80 | 99% | 2.17 | 2.87 | 5.05 | 120.0 → 116.2 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.65 | 3.04 | 17.39 | 99% | 0.65 | 2.58 | 4.58 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.60 | 13.68 | 100% | 0.44 | 2.30 | 4.06 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.223 |
| MeasuringLayoutable LineDrawing | 0.057 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.07 | 38.71 | 99% | 0.63 | 2.60 | 4.62 | 116.3 → 117.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.32 | 10.05 | 107.77 | 95% | 1.77 | 6.73 | 8.90 | 606.5 → 630.7 | 14 | 3.5 / 30 |

