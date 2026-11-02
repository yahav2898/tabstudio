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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.63 | 14.76 | 99% | 0.47 | 2.28 | 3.52 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 2.87 | 15.57 | 99% | 1.03 | 2.35 | 3.79 | 129.3 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 3.23 | 15.76 | 99% | 1.80 | 2.40 | 4.56 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.92 | 16.79 | 99% | 0.71 | 2.36 | 4.06 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.04 | 16.68 | 99% | 1.67 | 2.36 | 4.36 | 91.1 → 88.9 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 4.57 | 93.55 | 99% | 1.70 | 2.65 | 4.95 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.76 | 3.00 | 20.82 | 99% | 0.73 | 2.40 | 3.91 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.98 | 17.10 | 99% | 0.75 | 2.45 | 4.03 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 2.76 | 14.14 | 99% | 0.56 | 2.34 | 3.49 | 80.2 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 2.94 | 19.45 | 99% | 0.69 | 2.40 | 4.11 | 119.5 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.79 | 4.19 | 18.72 | 99% | 2.20 | 2.52 | 4.06 | 119.7 → 115.6 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.67 | 93.53 | 99% | 1.57 | 2.24 | 3.61 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.298 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.87 | 37.11 | 99% | 0.68 | 2.42 | 3.68 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.83 | 8.30 | 35.04 | 95% | 1.93 | 6.90 | 7.92 | 584.7 → 621.0 | 15 | 3.5 / 30 |

