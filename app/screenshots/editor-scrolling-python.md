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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.07 | 11.18 | 99% | 0.45 | 1.80 | 4.51 | 126.6 → 134.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.42 | 14.05 | 99% | 0.63 | 1.82 | 5.06 | 162.9 → 160.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.18 | 4.58 | 20.93 | 99% | 2.20 | 3.60 | 5.49 | 210.0 → 195.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.19 | 11.95 | 99% | 0.62 | 1.81 | 5.12 | 148.3 → 149.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.10 | 13.53 | 99% | 0.60 | 1.67 | 4.89 | 123.1 → 121.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 2.17 | 12.27 | 99% | 0.58 | 1.70 | 4.87 | 147.7 → 144.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 3.07 | 17.16 | 99% | 0.70 | 2.30 | 4.94 | 150.1 → 147.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.47 | 9.88 | 99% | 0.59 | 2.00 | 5.00 | 152.9 → 149.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.05 | 9.15 | 99% | 0.44 | 1.66 | 4.68 | 112.6 → 110.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.27 | 13.76 | 99% | 0.60 | 1.85 | 4.91 | 152.8 → 149.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.46 | 17.09 | 99% | 0.65 | 1.84 | 4.26 | 152.6 → 149.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.54 | 69.08 | 99% | 0.50 | 1.77 | 4.00 | 74.1 → 73.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.189 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 2.23 | 16.95 | 99% | 0.59 | 1.79 | 4.80 | 148.6 → 149.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.23 | 6.44 | 21.74 | 96% | 0.74 | 5.05 | 5.22 | 554.5 → 573.6 | 14 | 3.5 / 31 |

