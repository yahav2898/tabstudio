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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 1.90 | 10.84 | 100% | 0.38 | 1.64 | 3.69 | 124.3 → 131.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.13 | 12.75 | 99% | 0.54 | 1.75 | 3.94 | 161.5 → 159.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.69 | 2.28 | 13.05 | 99% | 0.72 | 1.80 | 4.04 | 208.8 → 193.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.99 | 8.53 | 100% | 0.58 | 2.27 | 4.01 | 146.5 → 148.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.77 | 10.70 | 100% | 0.55 | 2.18 | 4.03 | 121.5 → 119.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.06 | 14.78 | 99% | 0.54 | 1.75 | 3.83 | 145.7 → 142.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 1.67 | 12.88 | 99% | 0.49 | 1.29 | 3.84 | 147.7 → 144.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.80 | 12.47 | 99% | 0.54 | 1.38 | 4.07 | 150.7 → 147.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.72 | 14.21 | 99% | 0.50 | 1.97 | 3.72 | 109.9 → 108.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.98 | 12.65 | 99% | 0.53 | 2.40 | 4.28 | 151.3 → 147.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 1.63 | 13.09 | 99% | 0.49 | 1.29 | 3.56 | 150.3 → 147.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.26 | 7.61 | 100% | 0.35 | 1.06 | 3.17 | 70.5 → 70.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.205 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 1.59 | 12.90 | 99% | 0.49 | 1.27 | 3.84 | 146.4 → 147.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.22 | 7.97 | 33.89 | 95% | 2.55 | 5.18 | 4.70 | 555.7 → 574.1 | 13 | 3.5 / 31 |

