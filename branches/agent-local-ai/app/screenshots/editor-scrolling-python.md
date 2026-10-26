# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 2.48 | 9.97 | 100% | 0.49 | 2.05 | 4.50 | 123.1 → 130.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.01 | 3.62 | 18.69 | 99% | 0.84 | 2.80 | 5.10 | 160.6 → 158.0 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 3.50 | 16.43 | 99% | 1.05 | 2.70 | 5.44 | 208.5 → 192.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.55 | 13.76 | 99% | 0.66 | 2.02 | 4.88 | 144.9 → 146.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 4.35 | 11.74 | 99% | 0.92 | 2.58 | 5.44 | 119.8 → 118.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 2.78 | 15.17 | 99% | 0.65 | 2.35 | 4.83 | 144.4 → 140.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.94 | 15.08 | 99% | 0.66 | 2.43 | 4.87 | 146.6 → 143.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.28 | 15.12 | 99% | 0.69 | 2.61 | 4.94 | 149.8 → 146.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.71 | 9.98 | 99% | 0.51 | 2.29 | 4.56 | 109.2 → 107.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 2.53 | 13.97 | 99% | 0.63 | 2.08 | 4.77 | 149.1 → 146.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.55 | 13.18 | 99% | 0.63 | 2.17 | 4.39 | 149.3 → 146.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.57 | 7.62 | 100% | 0.55 | 2.19 | 4.15 | 70.0 → 69.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.204 |
| ArrangingLayoutable Rectangle | 0.055 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.67 | 14.67 | 99% | 0.62 | 2.23 | 4.67 | 145.2 → 146.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.89 | 12.18 | 75.94 | 93% | 1.03 | 11.57 | 6.74 | 555.3 → 584.5 | 14 | 3.5 / 31 |

