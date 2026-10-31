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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.32 | 17.51 | 99% | 0.54 | 2.83 | 4.68 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.57 | 18.15 | 99% | 0.93 | 2.87 | 4.99 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.83 | 18.33 | 99% | 1.77 | 2.87 | 5.50 | 176.8 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 4.97 | 25.85 | 99% | 2.06 | 2.97 | 5.92 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.43 | 15.54 | 99% | 1.63 | 2.71 | 5.35 | 91.6 → 89.2 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.72 | 3.29 | 14.23 | 99% | 0.75 | 2.76 | 5.24 | 115.5 → 110.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.39 | 19.25 | 99% | 0.78 | 2.77 | 5.25 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.78 | 3.55 | 17.82 | 99% | 0.96 | 2.86 | 5.25 | 120.2 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.79 | 3.42 | 14.25 | 99% | 1.68 | 2.79 | 5.29 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.83 | 3.51 | 16.56 | 99% | 0.81 | 2.84 | 4.98 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 3.25 | 16.14 | 99% | 0.71 | 2.71 | 4.09 | 120.0 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 3.04 | 9.80 | 100% | 0.57 | 2.52 | 4.42 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.385 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.022 |
| FindingResource - | 0.017 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 3.77 | 116.57 | 99% | 1.73 | 2.92 | 5.53 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.27 | 7.00 | 29.61 | 96% | 0.95 | 6.02 | 9.18 | 605.5 → 630.8 | 14 | 3.5 / 30 |

