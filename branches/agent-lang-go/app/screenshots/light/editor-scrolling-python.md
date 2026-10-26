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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.73 | 9.92 | 99% | 0.42 | 2.36 | 4.45 | 122.9 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 3.11 | 14.46 | 99% | 0.73 | 2.53 | 4.98 | 159.6 → 157.4 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 2.29 | 14.62 | 99% | 0.85 | 1.68 | 4.75 | 206.4 → 191.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.14 | 13.58 | 99% | 0.58 | 1.68 | 4.63 | 144.5 → 145.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 1.80 | 9.24 | 100% | 0.54 | 1.43 | 4.47 | 118.5 → 117.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 3.74 | 15.73 | 99% | 0.86 | 2.61 | 5.46 | 144.4 → 140.5 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.80 | 2.23 | 14.64 | 99% | 0.61 | 1.75 | 4.81 | 146.1 → 143.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.08 | 15.27 | 99% | 0.60 | 1.63 | 4.65 | 149.0 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 2.03 | 10.78 | 99% | 0.46 | 1.62 | 4.37 | 107.8 → 106.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 3.79 | 15.60 | 99% | 1.12 | 2.38 | 4.96 | 149.4 → 145.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.06 | 13.39 | 99% | 0.59 | 1.63 | 4.00 | 148.4 → 145.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.55 | 8.33 | 100% | 0.40 | 1.31 | 3.76 | 68.7 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 4.02 | 70.75 | 99% | 0.82 | 2.46 | 5.09 | 145.0 → 146.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.04 | 4.92 | 22.73 | 96% | 0.72 | 4.16 | 4.83 | 547.0 → 567.9 | 12 | 3.5 / 31 |

