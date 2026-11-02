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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.17 | 17.67 | 99% | 0.51 | 2.75 | 4.86 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 3.48 | 19.76 | 99% | 1.33 | 2.80 | 5.06 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.76 | 18.05 | 99% | 1.73 | 2.86 | 5.50 | 176.9 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 4.80 | 20.03 | 99% | 1.78 | 3.02 | 5.38 | 115.2 → 116.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.15 | 17.65 | 99% | 0.82 | 2.59 | 5.13 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 3.19 | 34.41 | 99% | 0.74 | 2.71 | 5.20 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 3.40 | 21.14 | 99% | 0.76 | 2.78 | 5.33 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.59 | 18.17 | 99% | 1.30 | 2.78 | 5.27 | 120.2 → 116.2 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 4.87 | 104.37 | 99% | 1.51 | 2.82 | 4.96 | 80.7 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.25 | 17.32 | 99% | 0.96 | 2.74 | 5.73 | 119.9 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.80 | 3.25 | 16.67 | 99% | 0.75 | 2.69 | 4.29 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.77 | 18.43 | 100% | 0.63 | 2.42 | 4.12 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.58 | 37.79 | 99% | 0.86 | 2.95 | 5.32 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.16 | 8.06 | 33.03 | 95% | 1.68 | 5.89 | 10.14 | 605.6 → 630.9 | 14 | 3.5 / 30 |

