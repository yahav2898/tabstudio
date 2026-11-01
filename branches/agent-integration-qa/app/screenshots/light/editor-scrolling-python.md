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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.13 | 17.41 | 100% | 0.49 | 2.73 | 4.63 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.44 | 17.99 | 99% | 1.00 | 2.84 | 4.78 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.87 | 18.64 | 99% | 1.72 | 2.87 | 5.51 | 176.1 → 162.2 | 5 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 4.40 | 18.86 | 99% | 1.65 | 3.08 | 5.44 | 114.9 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.13 | 15.58 | 99% | 0.70 | 2.61 | 5.04 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 3.26 | 34.81 | 99% | 0.75 | 2.71 | 5.12 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.73 | 3.30 | 19.44 | 99% | 0.71 | 2.75 | 5.16 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.49 | 21.37 | 99% | 1.66 | 2.87 | 5.01 | 119.8 → 115.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.71 | 3.38 | 13.07 | 99% | 1.34 | 2.78 | 4.99 | 80.4 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.74 | 3.51 | 16.31 | 99% | 0.78 | 2.81 | 5.34 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.28 | 18.17 | 99% | 0.80 | 2.70 | 4.53 | 119.7 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.72 | 17.35 | 100% | 0.54 | 2.40 | 4.12 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.378 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 3.15 | 35.61 | 99% | 0.67 | 2.65 | 4.54 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.10 | 7.18 | 25.75 | 95% | 0.88 | 5.69 | 8.83 | 600.0 → 626.1 | 13 | 3.5 / 30 |

