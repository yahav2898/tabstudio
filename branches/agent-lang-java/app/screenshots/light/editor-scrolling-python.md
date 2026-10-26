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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.41 | 10.43 | 99% | 0.51 | 1.96 | 4.52 | 122.9 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.11 | 3.46 | 75.42 | 99% | 0.89 | 2.57 | 5.23 | 159.8 → 157.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.16 | 2.98 | 15.37 | 99% | 1.02 | 2.07 | 5.17 | 206.7 → 191.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 2.52 | 13.43 | 99% | 0.70 | 1.99 | 4.64 | 144.5 → 146.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 2.34 | 11.13 | 99% | 0.71 | 1.80 | 5.08 | 119.0 → 117.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.97 | 4.17 | 77.25 | 99% | 0.87 | 2.49 | 5.22 | 144.8 → 140.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.92 | 2.50 | 13.31 | 99% | 0.73 | 1.97 | 4.70 | 146.1 → 143.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.94 | 2.49 | 17.05 | 99% | 0.70 | 1.95 | 4.71 | 149.1 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 2.50 | 10.91 | 99% | 0.61 | 1.89 | 4.60 | 108.1 → 106.9 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.05 | 3.02 | 77.05 | 99% | 0.80 | 2.08 | 5.15 | 149.5 → 145.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.91 | 2.44 | 13.14 | 99% | 0.72 | 1.91 | 4.45 | 148.9 → 145.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 1.99 | 12.51 | 100% | 0.52 | 1.58 | 3.95 | 68.9 → 68.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.339 |
| ArrangingLayoutable TextView | 0.031 |
| AttachingStyle - | 0.024 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 4.23 | 13.11 | 99% | 0.84 | 2.80 | 5.20 | 145.6 → 145.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.38 | 5.29 | 23.27 | 96% | 0.82 | 4.47 | 5.10 | 547.2 → 563.7 | 12 | 3.5 / 31 |

