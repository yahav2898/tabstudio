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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.21 | 11.45 | 99% | 0.47 | 1.81 | 3.88 | 121.8 → 129.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.30 | 12.89 | 100% | 0.64 | 1.75 | 4.25 | 158.3 → 155.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 3.88 | 75.84 | 99% | 2.53 | 2.55 | 4.51 | 205.0 → 190.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 1.99 | 9.65 | 99% | 0.52 | 1.65 | 3.92 | 143.5 → 144.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.02 | 12.41 | 100% | 0.58 | 1.57 | 4.03 | 118.0 → 116.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.04 | 13.65 | 99% | 0.57 | 1.60 | 3.90 | 143.2 → 138.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.25 | 12.26 | 99% | 0.60 | 1.70 | 4.21 | 144.7 → 142.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 3.13 | 17.39 | 99% | 2.20 | 2.44 | 4.16 | 148.4 → 145.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.01 | 9.36 | 99% | 0.42 | 1.59 | 3.78 | 107.2 → 105.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.09 | 12.72 | 99% | 0.56 | 1.64 | 3.90 | 147.6 → 144.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.23 | 11.94 | 99% | 0.61 | 1.71 | 3.57 | 147.6 → 144.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 1.82 | 7.04 | 100% | 0.44 | 1.45 | 3.29 | 69.0 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable ScrollContentPresenter | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 3.28 | 19.24 | 99% | 0.73 | 2.49 | 4.32 | 144.1 → 145.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.88 | 4.59 | 21.51 | 96% | 0.64 | 3.70 | 5.03 | 553.8 → 570.9 | 12 | 3.5 / 31 |

