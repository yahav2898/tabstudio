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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.76 | 3.34 | 18.78 | 99% | 0.53 | 2.84 | 4.79 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.49 | 17.33 | 99% | 0.96 | 2.83 | 5.04 | 129.8 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.90 | 19.34 | 99% | 1.82 | 2.85 | 5.78 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 4.73 | 20.89 | 99% | 1.95 | 3.01 | 5.88 | 115.2 → 116.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 3.39 | 14.90 | 99% | 0.90 | 2.69 | 5.46 | 91.6 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.90 | 3.47 | 20.60 | 99% | 0.81 | 2.91 | 5.37 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.86 | 3.58 | 18.49 | 99% | 0.81 | 2.86 | 5.66 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.95 | 3.71 | 17.49 | 99% | 0.82 | 2.99 | 5.42 | 120.2 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.89 | 4.27 | 15.81 | 100% | 1.73 | 2.97 | 5.88 | 80.7 → 78.4 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.95 | 3.67 | 18.67 | 99% | 0.88 | 2.95 | 5.72 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.85 | 3.44 | 21.11 | 99% | 0.80 | 2.87 | 4.63 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.91 | 11.95 | 100% | 0.53 | 2.49 | 4.27 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.343 |
| ArrangingLayoutable RepeatButton | 0.098 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.03 | 3.76 | 41.29 | 99% | 1.73 | 3.03 | 5.50 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.23 | 7.41 | 37.92 | 95% | 0.96 | 6.51 | 9.28 | 605.6 → 630.9 | 14 | 3.5 / 30 |

