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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 1.67 | 52.36 | 100% | 0.42 | 1.40 | 3.99 | 121.8 → 129.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 1.91 | 43.82 | 100% | 0.62 | 1.41 | 4.17 | 158.1 → 155.9 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 3.65 | 4.61 | 100% | 2.46 | 2.54 | 4.96 | 204.6 → 190.5 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 1.92 | 29.32 | 100% | 0.60 | 1.45 | 4.67 | 143.7 → 144.6 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 1.74 | 45.92 | 100% | 0.56 | 1.28 | 4.69 | 118.1 → 116.5 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 1.79 | 2.26 | 100% | 0.57 | 1.34 | 4.41 | 143.1 → 138.9 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 1.92 | 44.13 | 100% | 0.62 | 1.46 | 4.87 | 145.1 → 142.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.54 | 4.43 | 100% | 2.25 | 2.67 | 4.90 | 148.5 → 144.8 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 1.66 | 25.67 | 100% | 0.45 | 1.32 | 4.55 | 107.6 → 105.7 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 1.84 | 42.76 | 100% | 0.63 | 1.42 | 4.76 | 147.8 → 145.0 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 1.85 | 2.32 | 100% | 0.60 | 1.41 | 3.59 | 147.2 → 144.1 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.43 | 40.40 | 100% | 0.46 | 1.13 | 3.96 | 69.0 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.308 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable LineDrawing | 0.029 |
| ArrangingLayoutable TextView | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 1.89 | 28.00 | 100% | 0.60 | 1.43 | 4.77 | 143.9 → 144.6 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.08 | 3.82 | 63.57 | 99% | 0.69 | 3.29 | 5.04 | 555.1 → 571.1 | 3 | 3.5 / 31 |

