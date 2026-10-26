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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.32 | 11.23 | 99% | 0.53 | 1.82 | 3.55 | 121.6 → 129.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 2.41 | 14.57 | 99% | 0.68 | 1.79 | 3.64 | 157.6 → 155.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 3.62 | 16.08 | 99% | 1.08 | 2.40 | 3.85 | 204.0 → 189.6 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 3.05 | 13.15 | 99% | 0.77 | 1.91 | 3.73 | 143.4 → 144.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 2.20 | 12.41 | 99% | 0.68 | 1.69 | 3.47 | 117.5 → 116.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 2.43 | 16.14 | 99% | 0.59 | 1.98 | 3.57 | 142.4 → 138.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 2.55 | 15.82 | 99% | 0.69 | 2.00 | 3.68 | 145.0 → 141.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.03 | 3.21 | 13.20 | 99% | 0.76 | 2.44 | 3.72 | 148.0 → 144.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 2.98 | 74.29 | 99% | 0.55 | 2.45 | 3.34 | 107.6 → 105.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 2.92 | 14.15 | 99% | 0.68 | 2.32 | 3.58 | 147.6 → 144.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 2.74 | 15.02 | 99% | 0.67 | 2.19 | 3.26 | 147.2 → 143.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.38 | 10.16 | 100% | 0.54 | 1.98 | 2.79 | 68.7 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.249 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 4.48 | 72.86 | 99% | 0.96 | 2.68 | 3.79 | 143.7 → 145.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.08 | 5.17 | 24.77 | 96% | 0.70 | 4.00 | 4.06 | 547.2 → 565.5 | 12 | 3.5 / 31 |

