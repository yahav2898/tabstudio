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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.94 | 15.28 | 99% | 0.53 | 2.53 | 4.17 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 3.34 | 19.62 | 99% | 1.58 | 2.67 | 4.66 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.56 | 17.15 | 99% | 1.89 | 2.67 | 4.92 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.75 | 12.48 | 99% | 0.59 | 2.30 | 4.13 | 115.2 → 116.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.75 | 15.30 | 99% | 0.77 | 2.22 | 4.23 | 91.5 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.79 | 4.60 | 23.92 | 99% | 2.40 | 2.60 | 4.43 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.84 | 3.11 | 19.54 | 99% | 0.76 | 2.53 | 4.67 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.13 | 17.68 | 99% | 0.83 | 2.52 | 4.48 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.74 | 2.96 | 14.05 | 99% | 0.69 | 2.41 | 4.60 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.73 | 2.98 | 14.82 | 99% | 0.79 | 2.44 | 4.37 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.80 | 3.13 | 15.66 | 99% | 0.81 | 2.49 | 4.07 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.65 | 12.47 | 100% | 1.64 | 2.21 | 3.58 | 44.0 → 43.0 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.394 |
| AttachingStyle - | 0.047 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable ScrollContentPresenter | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.89 | 39.76 | 99% | 0.68 | 2.38 | 3.98 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.99 | 6.47 | 27.57 | 96% | 1.71 | 5.05 | 7.95 | 596.6 → 625.6 | 14 | 3.5 / 30 |

