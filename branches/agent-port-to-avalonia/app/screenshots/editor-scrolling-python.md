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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.04 | 10.42 | 99% | 0.40 | 1.77 | 3.75 | 122.3 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 3.27 | 18.00 | 99% | 0.75 | 2.48 | 4.11 | 159.8 → 157.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 2.39 | 16.74 | 99% | 0.78 | 1.91 | 4.10 | 206.7 → 191.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.25 | 13.77 | 99% | 0.55 | 1.91 | 3.93 | 144.3 → 145.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 2.03 | 13.74 | 99% | 0.51 | 1.69 | 3.75 | 118.6 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.92 | 16.41 | 99% | 0.51 | 1.68 | 3.85 | 143.9 → 140.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 4.47 | 16.41 | 99% | 0.70 | 2.59 | 4.28 | 146.8 → 143.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.25 | 17.28 | 99% | 0.54 | 1.92 | 4.04 | 149.3 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.98 | 8.88 | 99% | 0.39 | 1.71 | 3.61 | 107.6 → 105.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 2.19 | 13.13 | 99% | 0.51 | 1.85 | 3.79 | 148.4 → 145.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 2.20 | 13.40 | 99% | 0.53 | 1.85 | 3.47 | 148.4 → 145.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.42 | 11.95 | 99% | 0.44 | 2.17 | 3.29 | 69.2 → 68.9 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.206 |
| ArrangingLayoutable CachedLineNumberMargin | 0.061 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.17 | 15.66 | 99% | 0.55 | 1.79 | 3.85 | 144.6 → 145.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.68 | 4.04 | 30.96 | 96% | 0.59 | 3.57 | 4.04 | 552.3 → 572.9 | 13 | 3.5 / 31 |

