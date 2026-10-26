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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 2.28 | 12.99 | 99% | 0.48 | 1.89 | 5.31 | 122.1 → 129.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.11 | 3.91 | 16.05 | 99% | 0.89 | 2.47 | 5.46 | 158.9 → 156.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 2.80 | 16.39 | 99% | 0.99 | 2.02 | 5.29 | 205.4 → 190.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.28 | 14.54 | 99% | 0.65 | 1.80 | 4.81 | 143.8 → 145.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.04 | 10.57 | 100% | 0.59 | 1.60 | 4.82 | 118.6 → 117.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 4.20 | 17.45 | 99% | 0.94 | 2.57 | 5.37 | 143.9 → 140.2 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.36 | 14.63 | 99% | 0.64 | 1.80 | 4.75 | 145.5 → 142.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 2.24 | 15.62 | 99% | 0.64 | 1.81 | 4.85 | 148.4 → 145.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 2.38 | 9.44 | 99% | 0.60 | 1.87 | 4.94 | 108.1 → 106.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 4.38 | 14.24 | 99% | 0.89 | 2.44 | 5.33 | 149.0 → 145.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 2.27 | 16.21 | 99% | 0.64 | 1.78 | 4.50 | 148.1 → 144.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.74 | 13.44 | 100% | 0.47 | 1.44 | 4.00 | 69.2 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.257 |
| MeasuringLayoutable CachedLineNumberMargin | 0.042 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 4.16 | 77.38 | 99% | 0.92 | 2.67 | 5.54 | 144.7 → 145.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.28 | 5.33 | 23.84 | 96% | 0.77 | 4.63 | 5.14 | 552.6 → 567.5 | 12 | 3.5 / 31 |

