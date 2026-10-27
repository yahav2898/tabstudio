# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 3.35 | 12.41 | 100% | 0.47 | 2.90 | 4.60 | 131.3 → 138.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 5.35 | 18.46 | 99% | 0.85 | 3.31 | 5.44 | 168.1 → 167.3 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.82 | 17.96 | 99% | 0.91 | 3.13 | 5.29 | 214.7 → 199.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.45 | 15.08 | 99% | 0.66 | 2.97 | 4.92 | 153.3 → 154.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.32 | 15.79 | 99% | 0.62 | 2.80 | 4.93 | 127.4 → 126.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.95 | 5.31 | 16.35 | 99% | 0.85 | 4.59 | 5.47 | 153.3 → 149.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 3.51 | 15.09 | 99% | 0.67 | 3.00 | 4.94 | 155.1 → 151.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.38 | 15.54 | 99% | 0.63 | 2.89 | 4.95 | 157.5 → 154.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 3.29 | 11.43 | 99% | 0.53 | 2.82 | 4.90 | 117.0 → 115.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 5.38 | 80.48 | 98% | 0.79 | 3.26 | 5.32 | 159.1 → 154.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 3.40 | 16.75 | 99% | 0.62 | 2.89 | 4.38 | 157.0 → 153.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.88 | 12.47 | 100% | 0.47 | 2.56 | 4.05 | 79.0 → 78.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable TextView | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.06 | 5.02 | 21.18 | 99% | 0.92 | 3.38 | 5.79 | 153.9 → 156.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.24 | 6.08 | 23.74 | 96% | 0.78 | 5.24 | 5.43 | 556.3 → 571.5 | 13 | 3.5 / 31 |

