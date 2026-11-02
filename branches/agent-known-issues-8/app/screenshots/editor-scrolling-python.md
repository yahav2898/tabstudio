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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.22 | 16.15 | 99% | 0.52 | 2.83 | 4.51 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 3.64 | 15.12 | 99% | 1.36 | 2.88 | 4.90 | 129.8 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.78 | 18.43 | 99% | 1.72 | 2.87 | 5.43 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 3.62 | 17.95 | 99% | 1.60 | 2.96 | 5.03 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 4.43 | 15.53 | 99% | 1.60 | 2.83 | 5.20 | 91.6 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 3.37 | 20.11 | 99% | 0.77 | 2.77 | 4.91 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 3.47 | 20.27 | 99% | 1.11 | 2.82 | 5.53 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.28 | 16.72 | 99% | 0.70 | 2.78 | 5.23 | 120.2 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.68 | 3.35 | 13.04 | 100% | 0.63 | 2.75 | 4.53 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.85 | 4.80 | 16.54 | 99% | 1.86 | 2.92 | 5.36 | 120.1 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.76 | 3.32 | 17.40 | 99% | 0.73 | 2.75 | 4.10 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.73 | 12.11 | 100% | 0.55 | 2.38 | 4.09 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.289 |
| ArrangingLayoutable NumberDrawing | 0.086 |
| AttachingStyle - | 0.075 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 4.82 | 38.60 | 99% | 1.94 | 3.06 | 5.67 | 116.4 → 117.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.29 | 7.01 | 32.78 | 96% | 0.93 | 6.11 | 8.98 | 604.7 → 629.7 | 14 | 3.5 / 30 |

