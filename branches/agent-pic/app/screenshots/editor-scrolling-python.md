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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 1.79 | 2.23 | 100% | 0.45 | 1.49 | 4.10 | 122.9 → 130.4 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.33 | 48.73 | 100% | 0.72 | 1.56 | 4.63 | 159.5 → 157.4 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.74 | 11.19 | 100% | 1.02 | 2.49 | 5.12 | 207.5 → 191.8 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 1.91 | 40.96 | 100% | 0.65 | 1.49 | 4.87 | 144.7 → 146.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 1.70 | 2.25 | 100% | 0.58 | 1.28 | 4.65 | 119.3 → 117.2 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 1.82 | 41.21 | 100% | 0.62 | 1.38 | 4.76 | 144.2 → 140.2 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.83 | 13.27 | 100% | 2.39 | 2.56 | 5.13 | 146.5 → 143.7 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 1.86 | 4.69 | 100% | 0.62 | 1.43 | 4.63 | 149.6 → 146.1 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 1.76 | 38.60 | 100% | 0.45 | 1.36 | 4.44 | 108.1 → 106.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 1.86 | 2.22 | 100% | 0.59 | 1.43 | 4.68 | 149.0 → 146.0 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 1.92 | 40.99 | 100% | 0.62 | 1.47 | 4.39 | 148.6 → 145.5 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.58 | 5.16 | 100% | 0.53 | 2.21 | 3.97 | 69.4 → 68.9 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 1.89 | 39.91 | 100% | 0.62 | 1.48 | 4.63 | 145.1 → 146.0 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.11 | 3.94 | 61.65 | 99% | 0.70 | 3.50 | 5.08 | 555.2 → 573.4 | 2 | 3.5 / 31 |

