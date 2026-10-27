# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.63 | 11.61 | 99% | 0.39 | 2.34 | 4.21 | 131.0 → 138.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.49 | 14.45 | 99% | 0.61 | 2.27 | 4.94 | 167.6 → 165.2 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.22 | 5.32 | 16.78 | 98% | 2.62 | 4.35 | 5.65 | 215.4 → 200.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.71 | 14.19 | 99% | 0.58 | 2.39 | 4.77 | 152.8 → 154.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.55 | 12.13 | 99% | 0.57 | 2.23 | 4.85 | 127.9 → 126.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.65 | 13.96 | 99% | 0.58 | 2.34 | 5.01 | 152.5 → 148.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.89 | 3.56 | 14.84 | 99% | 0.69 | 2.75 | 4.98 | 154.7 → 152.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.85 | 14.58 | 99% | 0.58 | 2.42 | 4.96 | 157.4 → 154.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.46 | 11.13 | 99% | 0.45 | 2.26 | 4.61 | 117.0 → 115.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.87 | 11.81 | 99% | 0.58 | 2.45 | 4.87 | 156.9 → 154.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.23 | 13.30 | 99% | 0.66 | 2.61 | 4.36 | 157.2 → 154.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.51 | 79.11 | 99% | 0.47 | 2.20 | 4.02 | 79.3 → 77.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.69 | 14.09 | 99% | 0.58 | 2.36 | 4.83 | 153.2 → 154.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.16 | 6.43 | 28.05 | 96% | 0.70 | 5.13 | 5.09 | 559.6 → 576.2 | 14 | 3.5 / 31 |

