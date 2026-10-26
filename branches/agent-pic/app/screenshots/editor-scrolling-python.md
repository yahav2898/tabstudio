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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.07 | 9.56 | 99% | 0.42 | 1.79 | 4.31 | 123.4 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.91 | 17.65 | 99% | 0.71 | 1.99 | 5.04 | 159.8 → 158.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.68 | 15.09 | 98% | 1.00 | 2.56 | 5.22 | 207.4 → 192.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.17 | 13.33 | 99% | 0.57 | 1.71 | 4.65 | 144.9 → 146.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.05 | 12.37 | 99% | 0.56 | 1.59 | 4.67 | 119.3 → 117.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.33 | 18.96 | 99% | 0.73 | 2.25 | 5.29 | 144.5 → 141.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.55 | 14.56 | 99% | 0.66 | 1.96 | 5.08 | 146.7 → 143.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.31 | 14.30 | 99% | 0.63 | 1.84 | 4.74 | 149.4 → 146.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.03 | 13.06 | 100% | 0.46 | 1.71 | 4.62 | 108.6 → 106.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.12 | 16.32 | 99% | 0.75 | 2.61 | 4.97 | 149.4 → 146.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 2.18 | 11.85 | 99% | 0.60 | 1.72 | 4.20 | 149.1 → 145.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.71 | 14.49 | 100% | 0.41 | 1.43 | 3.91 | 69.2 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.256 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.34 | 11.60 | 99% | 0.60 | 1.82 | 4.92 | 145.2 → 146.1 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.60 | 9.26 | 23.80 | 95% | 0.82 | 5.89 | 5.60 | 561.4 → 574.7 | 12 | 3.5 / 31 |

