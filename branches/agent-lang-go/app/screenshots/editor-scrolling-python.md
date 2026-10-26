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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.11 | 13.02 | 99% | 0.46 | 1.77 | 4.49 | 123.1 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 2.45 | 14.12 | 99% | 0.71 | 1.88 | 4.83 | 159.9 → 157.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 4.78 | 17.45 | 99% | 2.76 | 2.82 | 5.51 | 208.1 → 192.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.88 | 2.45 | 16.14 | 99% | 0.69 | 1.93 | 4.90 | 145.1 → 146.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.05 | 10.61 | 99% | 0.65 | 1.60 | 4.69 | 119.4 → 117.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 2.80 | 17.12 | 99% | 0.70 | 2.02 | 5.43 | 144.5 → 141.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 3.02 | 14.53 | 99% | 0.78 | 2.20 | 5.27 | 147.2 → 143.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.91 | 2.43 | 14.30 | 99% | 0.70 | 1.87 | 4.87 | 149.5 → 146.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 2.26 | 10.51 | 99% | 0.56 | 1.82 | 4.50 | 108.4 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 3.75 | 14.69 | 99% | 2.49 | 2.47 | 5.29 | 149.8 → 146.9 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 2.36 | 15.34 | 99% | 0.69 | 1.83 | 4.45 | 149.2 → 146.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 1.82 | 9.72 | 99% | 0.50 | 1.48 | 3.90 | 69.6 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.223 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.85 | 17.82 | 99% | 0.81 | 2.66 | 5.33 | 145.3 → 146.9 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 6.34 | 24.46 | 96% | 0.75 | 4.66 | 5.22 | 552.1 → 572.0 | 12 | 3.5 / 31 |

