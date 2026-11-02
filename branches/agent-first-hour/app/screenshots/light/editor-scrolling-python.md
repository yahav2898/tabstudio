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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.78 | 17.92 | 99% | 0.40 | 2.53 | 4.05 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 3.16 | 19.67 | 100% | 1.00 | 2.65 | 4.90 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 3.23 | 18.42 | 99% | 1.47 | 2.63 | 5.14 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.60 | 18.84 | 99% | 1.58 | 2.81 | 5.24 | 114.8 → 115.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 3.82 | 17.15 | 99% | 1.48 | 2.56 | 5.00 | 91.2 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.55 | 2.94 | 18.01 | 99% | 0.62 | 2.52 | 4.43 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 2.94 | 19.79 | 99% | 0.68 | 2.53 | 4.62 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.71 | 3.44 | 17.98 | 99% | 0.76 | 2.74 | 5.60 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.65 | 3.26 | 17.28 | 100% | 1.43 | 2.71 | 4.49 | 80.3 → 78.0 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 3.83 | 18.97 | 99% | 1.38 | 2.77 | 5.09 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.64 | 3.11 | 18.20 | 99% | 0.69 | 2.63 | 4.51 | 119.7 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.58 | 14.87 | 100% | 0.47 | 2.29 | 4.08 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.226 |
| ArrangingLayoutable TextView | 0.107 |
| ArrangingLayoutable ScrollingTextLayer | 0.061 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 4.27 | 40.00 | 99% | 1.65 | 2.95 | 5.06 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.41 | 6.65 | 32.74 | 96% | 0.91 | 5.47 | 8.43 | 593.7 → 619.8 | 14 | 3.5 / 30 |

