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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.06 | 3.73 | 19.31 | 99% | 0.70 | 3.10 | 5.49 | 93.3 → 100.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 4.98 | 20.30 | 99% | 2.70 | 3.17 | 5.95 | 129.3 → 129.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.42 | 4.16 | 20.02 | 99% | 2.00 | 3.11 | 6.12 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.16 | 3.84 | 17.61 | 99% | 0.91 | 3.03 | 5.22 | 114.8 → 115.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.96 | 3.68 | 18.15 | 99% | 1.76 | 2.89 | 5.79 | 91.1 → 89.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 1.22 | 3.92 | 20.60 | 99% | 1.92 | 3.13 | 6.04 | 115.1 → 110.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.22 | 3.97 | 19.17 | 99% | 0.99 | 3.14 | 5.37 | 117.1 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.20 | 3.89 | 17.34 | 99% | 0.95 | 3.06 | 5.29 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 1.07 | 3.80 | 21.15 | 99% | 1.57 | 3.06 | 5.69 | 80.3 → 78.1 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.18 | 3.82 | 17.65 | 99% | 0.96 | 3.04 | 5.45 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.13 | 3.89 | 17.94 | 99% | 1.06 | 3.04 | 4.76 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.76 | 3.37 | 13.75 | 100% | 1.69 | 2.73 | 4.91 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.524 |
| ArrangingLayoutable NumberDrawing | 0.066 |
| ArrangingLayoutable TextView | 0.037 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable LineDrawing | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.024 |
| FindingResource - | 0.017 |
| MeasuringLayoutable NumberDrawing | 0.014 |
| MeasuringLayoutable Grid | 0.014 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.010 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.21 | 3.79 | 39.69 | 99% | 0.97 | 2.96 | 5.42 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.38 | 14.97 | 42.57 | 87% | 2.68 | 9.51 | 11.08 | 605.1 → 625.1 | 15 | 3.5 / 30 |

