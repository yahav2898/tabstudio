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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 3.58 | 17.65 | 99% | 0.61 | 3.01 | 4.88 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.09 | 3.81 | 18.43 | 99% | 1.52 | 3.05 | 4.91 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 4.21 | 19.14 | 99% | 1.75 | 3.01 | 5.75 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 4.73 | 15.37 | 99% | 1.82 | 3.06 | 5.68 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.32 | 16.42 | 100% | 0.78 | 2.71 | 5.37 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.80 | 3.36 | 30.91 | 99% | 0.75 | 2.75 | 5.35 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 3.42 | 16.14 | 99% | 1.05 | 2.83 | 5.44 | 117.4 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.86 | 3.73 | 18.48 | 99% | 2.22 | 2.88 | 4.90 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.66 | 3.19 | 12.32 | 99% | 1.26 | 2.70 | 4.93 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.36 | 16.98 | 99% | 0.77 | 2.78 | 4.66 | 119.9 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.79 | 3.30 | 17.00 | 99% | 0.72 | 2.71 | 4.12 | 120.0 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.70 | 16.76 | 100% | 0.48 | 2.36 | 4.13 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.344 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.58 | 40.38 | 99% | 0.89 | 2.93 | 5.23 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.63 | 8.34 | 26.86 | 95% | 1.20 | 6.73 | 9.48 | 607.8 → 631.8 | 14 | 3.5 / 30 |

