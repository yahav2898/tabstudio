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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.20 | 18.91 | 99% | 0.48 | 1.77 | 5.08 | 121.9 → 129.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 3.42 | 61.24 | 99% | 0.78 | 2.52 | 5.83 | 159.1 → 155.9 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 2.82 | 18.92 | 99% | 0.98 | 2.00 | 5.86 | 205.2 → 190.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 2.42 | 18.79 | 99% | 0.68 | 1.92 | 5.54 | 143.5 → 145.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 3.35 | 17.91 | 99% | 0.72 | 2.46 | 5.34 | 119.0 → 117.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.34 | 19.53 | 99% | 0.67 | 1.87 | 5.36 | 142.9 → 139.2 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.39 | 19.48 | 99% | 0.66 | 1.90 | 5.36 | 145.2 → 141.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 3.30 | 62.86 | 99% | 0.90 | 2.53 | 5.94 | 148.4 → 145.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.73 | 2.25 | 16.48 | 99% | 0.50 | 1.80 | 5.33 | 108.1 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 2.41 | 18.17 | 99% | 0.69 | 1.87 | 5.30 | 147.7 → 144.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 2.89 | 17.35 | 99% | 0.77 | 2.11 | 4.99 | 148.1 → 145.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.04 | 7.73 | 100% | 0.51 | 1.61 | 4.52 | 69.6 → 68.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 2.56 | 19.43 | 99% | 0.70 | 2.02 | 5.60 | 144.2 → 145.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.83 | 6.70 | 73.32 | 96% | 0.81 | 5.97 | 6.26 | 549.4 → 564.2 | 9 | 3.5 / 31 |

