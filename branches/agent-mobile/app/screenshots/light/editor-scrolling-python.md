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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.63 | 11.48 | 99% | 0.57 | 2.08 | 4.91 | 122.9 → 130.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 3.75 | 75.08 | 99% | 0.88 | 2.59 | 5.24 | 160.3 → 157.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.23 | 2.89 | 16.66 | 99% | 1.01 | 2.09 | 5.42 | 207.2 → 192.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 3.19 | 13.10 | 99% | 0.71 | 2.64 | 5.08 | 145.1 → 146.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 3.40 | 9.14 | 100% | 0.75 | 2.65 | 5.32 | 120.1 → 117.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 2.95 | 16.22 | 99% | 0.72 | 2.39 | 5.08 | 144.6 → 140.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 2.87 | 13.24 | 99% | 0.68 | 2.29 | 5.03 | 146.6 → 143.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.08 | 3.64 | 70.52 | 99% | 2.62 | 2.64 | 5.48 | 149.9 → 147.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 2.81 | 12.99 | 99% | 0.52 | 2.36 | 4.74 | 108.3 → 106.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 3.07 | 13.67 | 99% | 0.74 | 2.53 | 4.94 | 149.1 → 146.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.98 | 3.43 | 13.42 | 99% | 0.83 | 2.71 | 4.73 | 149.0 → 146.6 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.22 | 6.68 | 100% | 0.49 | 1.78 | 4.11 | 69.8 → 69.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.241 |
| ArrangingLayoutable TextView | 0.051 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 2.99 | 15.57 | 99% | 0.68 | 2.52 | 5.13 | 145.1 → 146.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.42 | 8.98 | 30.74 | 94% | 2.43 | 6.80 | 5.74 | 550.9 → 568.9 | 14 | 3.5 / 31 |

