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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.62 | 9.31 | 100% | 0.48 | 2.24 | 4.56 | 122.9 → 130.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 4.50 | 21.51 | 99% | 0.89 | 2.83 | 5.15 | 161.0 → 157.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 3.17 | 15.62 | 99% | 0.93 | 2.49 | 5.02 | 207.4 → 192.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.95 | 14.93 | 99% | 0.65 | 2.47 | 4.84 | 145.0 → 146.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 3.73 | 77.46 | 99% | 1.07 | 2.50 | 5.03 | 119.8 → 118.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.75 | 14.90 | 99% | 0.65 | 2.26 | 4.60 | 144.7 → 140.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.88 | 13.38 | 99% | 0.67 | 2.42 | 4.85 | 146.8 → 143.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.13 | 17.14 | 99% | 0.69 | 2.53 | 4.88 | 149.4 → 146.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 2.95 | 76.68 | 99% | 0.54 | 2.50 | 4.71 | 109.4 → 106.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 2.99 | 12.90 | 99% | 0.66 | 2.50 | 4.84 | 149.5 → 146.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.41 | 14.96 | 99% | 0.65 | 1.86 | 4.28 | 149.4 → 146.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.73 | 15.08 | 100% | 0.65 | 2.28 | 4.54 | 70.3 → 69.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| AttachingStyle - | 0.047 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 2.74 | 14.98 | 99% | 0.65 | 2.36 | 4.72 | 145.2 → 146.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.47 | 7.48 | 29.67 | 95% | 0.87 | 6.60 | 5.02 | 551.8 → 576.0 | 14 | 3.5 / 31 |

