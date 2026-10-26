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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.93 | 3.56 | 15.69 | 99% | 0.58 | 3.00 | 5.16 | 123.2 → 130.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.79 | 16.65 | 99% | 0.86 | 3.06 | 5.53 | 159.7 → 157.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.31 | 4.19 | 19.46 | 99% | 1.17 | 3.01 | 5.58 | 206.4 → 191.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 3.55 | 13.73 | 99% | 0.72 | 2.86 | 5.19 | 144.9 → 145.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 3.09 | 16.29 | 99% | 0.64 | 2.65 | 5.03 | 118.8 → 117.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 3.14 | 17.18 | 99% | 0.65 | 2.67 | 5.01 | 143.5 → 139.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 3.46 | 17.09 | 99% | 0.77 | 2.86 | 5.02 | 146.0 → 143.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 4.44 | 17.49 | 99% | 0.78 | 3.11 | 5.43 | 149.7 → 145.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.15 | 13.39 | 99% | 0.54 | 2.69 | 4.97 | 108.2 → 106.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 3.09 | 18.84 | 99% | 0.66 | 2.68 | 5.03 | 148.2 → 145.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.19 | 16.42 | 99% | 0.66 | 2.72 | 4.46 | 148.4 → 145.0 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 2.88 | 8.62 | 100% | 0.60 | 2.44 | 4.30 | 70.2 → 69.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.347 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 3.10 | 15.07 | 99% | 0.65 | 2.65 | 4.70 | 144.5 → 145.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.28 | 6.05 | 32.09 | 96% | 0.74 | 5.31 | 5.20 | 549.6 → 568.0 | 13 | 3.5 / 31 |

