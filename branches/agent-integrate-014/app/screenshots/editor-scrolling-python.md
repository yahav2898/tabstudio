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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 2.64 | 14.28 | 99% | 0.63 | 2.12 | 4.81 | 122.4 → 129.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 2.88 | 14.63 | 99% | 0.81 | 2.16 | 4.92 | 158.7 → 156.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 4.86 | 13.11 | 99% | 2.84 | 2.75 | 5.22 | 206.2 → 191.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.98 | 2.62 | 17.19 | 99% | 0.71 | 1.99 | 4.63 | 143.8 → 145.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.31 | 11.01 | 99% | 0.66 | 1.78 | 4.54 | 118.7 → 117.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 2.60 | 18.31 | 99% | 0.74 | 1.94 | 4.83 | 143.2 → 139.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 3.19 | 15.93 | 99% | 0.81 | 2.17 | 4.90 | 145.6 → 142.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.22 | 15.77 | 99% | 0.76 | 2.54 | 5.06 | 149.3 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 2.42 | 12.80 | 99% | 0.61 | 1.94 | 4.67 | 108.2 → 106.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.04 | 2.70 | 15.28 | 99% | 0.75 | 2.04 | 4.81 | 148.1 → 145.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.92 | 2.52 | 14.57 | 99% | 0.66 | 1.91 | 4.05 | 148.1 → 144.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.43 | 11.75 | 100% | 0.55 | 1.60 | 3.72 | 69.4 → 68.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.374 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable RepeatButton | 0.050 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.014 |
| MeasuringLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| MeasuringLayoutable Border | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.99 | 2.64 | 14.70 | 99% | 0.74 | 1.95 | 4.74 | 144.2 → 145.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.15 | 5.26 | 31.69 | 96% | 0.76 | 4.09 | 4.89 | 550.6 → 569.5 | 13 | 3.5 / 31 |

