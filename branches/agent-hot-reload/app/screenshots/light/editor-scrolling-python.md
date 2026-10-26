# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 3.89 | 12.25 | 99% | 0.50 | 3.46 | 3.17 | 121.6 → 129.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.52 | 2.29 | 13.04 | 99% | 0.54 | 1.78 | 3.05 | 158.3 → 155.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.51 | 2.04 | 12.51 | 99% | 0.52 | 1.71 | 2.88 | 204.9 → 190.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.32 | 12.74 | 99% | 0.53 | 1.91 | 3.21 | 143.6 → 144.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.21 | 8.03 | 100% | 0.52 | 1.82 | 3.09 | 117.9 → 116.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 2.15 | 12.52 | 99% | 0.50 | 1.81 | 3.02 | 143.0 → 139.1 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 3.10 | 14.90 | 99% | 0.69 | 2.43 | 3.25 | 145.4 → 142.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 2.21 | 13.12 | 99% | 0.51 | 1.84 | 3.07 | 148.0 → 144.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.90 | 8.80 | 100% | 0.39 | 1.69 | 2.68 | 106.8 → 105.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.24 | 23.40 | 99% | 0.51 | 1.84 | 3.18 | 147.7 → 144.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.23 | 12.68 | 99% | 0.53 | 1.89 | 2.82 | 147.6 → 144.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 1.81 | 8.51 | 100% | 0.38 | 1.62 | 2.39 | 67.9 → 67.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.217 |
| AttachingStyle - | 0.083 |
| FindingResource - | 0.021 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| ArrangingLayoutable Track | 0.016 |
| ArrangingLayoutable ScrollContentPresenter | 0.014 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 3.03 | 12.90 | 99% | 0.70 | 2.45 | 3.57 | 144.4 → 145.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.56 | 4.82 | 30.90 | 96% | 0.62 | 3.85 | 4.02 | 543.8 → 565.0 | 12 | 3.5 / 31 |

