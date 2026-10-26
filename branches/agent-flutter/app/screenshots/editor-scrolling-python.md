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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.06 | 11.94 | 99% | 0.48 | 1.60 | 4.00 | 122.8 → 130.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.24 | 79.74 | 99% | 0.87 | 2.36 | 4.49 | 159.7 → 157.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 2.53 | 15.50 | 99% | 0.90 | 1.75 | 4.40 | 207.0 → 192.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 2.19 | 14.13 | 99% | 0.68 | 1.66 | 4.22 | 144.6 → 146.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.07 | 11.19 | 99% | 0.63 | 1.53 | 3.98 | 119.1 → 117.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.21 | 13.19 | 99% | 0.64 | 1.62 | 4.21 | 144.1 → 140.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 4.26 | 16.35 | 99% | 2.37 | 2.60 | 4.84 | 146.6 → 143.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 2.25 | 19.62 | 99% | 0.66 | 1.70 | 4.10 | 149.3 → 146.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 1.72 | 9.02 | 99% | 0.46 | 1.39 | 3.66 | 107.5 → 105.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.21 | 12.52 | 99% | 0.63 | 1.56 | 4.18 | 148.5 → 146.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 2.27 | 13.17 | 99% | 0.65 | 1.62 | 4.01 | 148.8 → 145.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 2.60 | 75.13 | 99% | 0.58 | 2.23 | 3.67 | 69.3 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| AttachingStyle - | 0.063 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.08 | 12.81 | 99% | 0.63 | 1.56 | 4.16 | 145.2 → 145.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.95 | 4.57 | 24.85 | 96% | 0.78 | 3.98 | 4.51 | 550.9 → 577.9 | 13 | 3.5 / 31 |

