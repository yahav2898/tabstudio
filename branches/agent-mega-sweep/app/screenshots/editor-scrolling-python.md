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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.93 | 3.56 | 16.76 | 99% | 0.63 | 2.93 | 4.99 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.23 | 3.79 | 18.35 | 99% | 1.12 | 2.99 | 5.29 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.47 | 5.35 | 19.29 | 99% | 2.56 | 3.11 | 6.04 | 176.9 → 162.8 | 5 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 3.68 | 16.94 | 99% | 0.96 | 2.99 | 5.75 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 3.54 | 17.32 | 100% | 0.89 | 2.80 | 5.66 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 1.08 | 3.72 | 21.07 | 99% | 0.95 | 2.91 | 5.25 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.19 | 3.90 | 17.33 | 99% | 1.73 | 3.07 | 5.73 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.07 | 3.84 | 26.01 | 99% | 1.50 | 2.98 | 5.43 | 120.3 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.92 | 3.41 | 15.83 | 99% | 0.70 | 2.83 | 4.58 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.04 | 3.65 | 17.32 | 99% | 0.91 | 2.91 | 5.72 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.02 | 3.75 | 14.27 | 99% | 0.99 | 2.96 | 5.06 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 3.36 | 103.32 | 99% | 1.65 | 2.69 | 4.78 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.395 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.023 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 3.77 | 39.80 | 99% | 0.89 | 3.01 | 5.07 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.42 | 13.69 | 109.32 | 86% | 2.37 | 11.67 | 10.54 | 612.3 → 632.0 | 15 | 3.5 / 30 |

