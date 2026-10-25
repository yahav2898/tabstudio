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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.02 | 11.12 | 99% | 0.47 | 1.72 | 4.36 | 123.1 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.51 | 75.77 | 99% | 0.89 | 2.60 | 5.31 | 159.9 → 158.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.56 | 14.36 | 99% | 0.94 | 1.81 | 4.86 | 207.0 → 192.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.26 | 12.68 | 99% | 0.63 | 1.79 | 4.53 | 144.9 → 146.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.18 | 10.75 | 99% | 0.65 | 1.68 | 4.64 | 119.3 → 117.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 4.30 | 13.85 | 99% | 1.83 | 2.66 | 5.06 | 144.6 → 141.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.26 | 12.51 | 99% | 0.65 | 1.77 | 4.55 | 146.6 → 143.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 2.18 | 15.34 | 99% | 0.63 | 1.72 | 4.70 | 149.4 → 146.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.09 | 9.52 | 100% | 0.52 | 1.64 | 4.41 | 108.3 → 106.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 3.29 | 14.52 | 99% | 0.71 | 2.51 | 5.36 | 150.1 → 146.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.14 | 14.44 | 99% | 0.61 | 1.69 | 4.18 | 149.1 → 146.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.75 | 8.57 | 100% | 0.44 | 1.41 | 3.81 | 69.3 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.252 |
| ArrangingLayoutable CachedLineNumberMargin | 0.051 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.34 | 23.33 | 99% | 0.94 | 2.45 | 5.22 | 145.6 → 147.1 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 5.61 | 23.44 | 96% | 0.81 | 4.71 | 5.12 | 556.9 → 572.3 | 13 | 3.5 / 31 |

