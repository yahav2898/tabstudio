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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.96 | 10.30 | 99% | 0.35 | 1.69 | 3.27 | 119.9 → 127.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 2.15 | 14.00 | 99% | 0.52 | 1.80 | 3.75 | 156.9 → 154.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.50 | 15.73 | 99% | 0.73 | 1.95 | 3.86 | 204.0 → 189.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.05 | 62.24 | 99% | 0.65 | 2.32 | 3.89 | 142.5 → 143.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 1.93 | 12.23 | 100% | 0.46 | 1.65 | 3.64 | 116.3 → 114.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 1.89 | 14.01 | 99% | 0.49 | 1.69 | 3.56 | 141.2 → 137.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 1.91 | 12.58 | 99% | 0.51 | 1.70 | 3.68 | 143.8 → 140.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 3.97 | 12.96 | 99% | 0.81 | 2.49 | 4.19 | 146.8 → 143.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.49 | 1.93 | 8.35 | 100% | 0.38 | 1.70 | 3.42 | 105.4 → 103.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.36 | 12.19 | 99% | 0.52 | 1.92 | 3.68 | 146.5 → 143.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.57 | 2.11 | 10.63 | 99% | 0.51 | 1.79 | 3.26 | 146.1 → 142.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.83 | 7.59 | 100% | 0.37 | 1.59 | 3.09 | 66.9 → 66.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.287 |
| ArrangingLayoutable CachedLineNumberMargin | 0.039 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| ArrangingLayoutable NumberDrawing | 0.026 |
| ArrangingLayoutable TextView | 0.015 |
| ArrangingLayoutable ScrollContentPresenter | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.59 | 12.30 | 99% | 0.55 | 2.07 | 3.83 | 142.8 → 143.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 4.86 | 20.29 | 96% | 0.58 | 3.69 | 3.85 | 549.6 → 569.1 | 13 | 3.5 / 31 |

