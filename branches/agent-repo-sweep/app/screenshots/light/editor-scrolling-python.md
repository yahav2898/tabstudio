# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.83 | 15.58 | 99% | 0.45 | 2.49 | 4.10 | 95.3 → 102.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.67 | 4.07 | 15.96 | 99% | 1.54 | 2.49 | 4.18 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 3.11 | 17.77 | 99% | 1.47 | 2.30 | 4.24 | 177.9 → 163.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.70 | 14.54 | 99% | 0.63 | 2.34 | 4.00 | 116.5 → 117.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.54 | 26.35 | 100% | 0.60 | 2.15 | 4.05 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.53 | 2.65 | 16.31 | 99% | 0.57 | 2.21 | 3.93 | 116.4 → 112.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 4.58 | 14.69 | 99% | 1.86 | 2.67 | 4.70 | 118.5 → 115.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.55 | 2.61 | 14.68 | 99% | 0.74 | 2.27 | 3.91 | 121.4 → 117.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 2.46 | 11.32 | 99% | 0.39 | 2.17 | 3.76 | 81.2 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 2.68 | 16.62 | 99% | 0.60 | 2.29 | 4.15 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.52 | 2.58 | 13.91 | 99% | 0.58 | 2.25 | 3.45 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.46 | 10.65 | 100% | 1.53 | 2.15 | 3.42 | 43.3 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.310 |
| ArrangingLayoutable CachedLineNumberMargin | 0.064 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable Track | 0.016 |
| ArrangingLayoutable VisualLayerManager | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.71 | 14.58 | 99% | 0.53 | 2.32 | 3.81 | 117.2 → 117.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.84 | 5.45 | 22.70 | 96% | 0.72 | 4.78 | 6.76 | 590.0 → 607.3 | 14 | 3.5 / 31 |

