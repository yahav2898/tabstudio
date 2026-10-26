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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.34 | 10.36 | 99% | 0.49 | 1.91 | 4.95 | 122.3 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 4.03 | 17.65 | 99% | 0.94 | 2.71 | 6.10 | 159.0 → 157.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 2.75 | 14.70 | 99% | 0.95 | 2.04 | 5.25 | 205.3 → 190.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.37 | 13.76 | 99% | 0.66 | 1.88 | 4.86 | 143.8 → 145.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.33 | 12.05 | 99% | 0.67 | 1.80 | 5.24 | 118.7 → 117.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 4.40 | 13.09 | 99% | 1.01 | 2.74 | 5.56 | 143.7 → 140.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.36 | 14.01 | 99% | 0.63 | 1.86 | 4.82 | 145.5 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.29 | 13.91 | 99% | 0.63 | 1.84 | 4.83 | 148.3 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.07 | 9.62 | 99% | 0.46 | 1.69 | 4.62 | 108.1 → 106.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 3.57 | 74.08 | 99% | 1.13 | 2.54 | 4.98 | 148.5 → 145.4 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 2.42 | 16.26 | 99% | 0.69 | 1.88 | 4.20 | 148.0 → 144.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.02 | 9.17 | 99% | 0.49 | 1.63 | 3.96 | 69.5 → 68.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.293 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.27 | 14.56 | 99% | 0.77 | 2.42 | 5.06 | 144.2 → 145.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.46 | 5.39 | 35.84 | 96% | 0.80 | 5.00 | 5.20 | 551.2 → 568.9 | 13 | 3.5 / 31 |

