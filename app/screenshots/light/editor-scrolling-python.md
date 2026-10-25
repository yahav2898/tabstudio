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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.58 | 9.02 | 100% | 0.38 | 1.27 | 3.49 | 121.8 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 1.69 | 12.28 | 99% | 0.54 | 1.33 | 3.66 | 158.3 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 2.21 | 13.10 | 99% | 0.73 | 1.49 | 3.90 | 205.4 → 190.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 3.07 | 10.97 | 99% | 0.71 | 2.32 | 4.05 | 144.2 → 145.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.45 | 9.51 | 99% | 0.48 | 1.14 | 3.55 | 118.0 → 116.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 1.61 | 13.09 | 99% | 0.48 | 1.25 | 3.55 | 143.2 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 1.56 | 11.15 | 99% | 0.48 | 1.26 | 3.55 | 145.1 → 141.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.55 | 1.56 | 10.89 | 99% | 0.49 | 1.26 | 3.63 | 148.0 → 144.7 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 3.53 | 9.00 | 99% | 0.55 | 2.46 | 4.20 | 107.6 → 105.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 1.58 | 12.76 | 99% | 0.50 | 1.23 | 3.69 | 147.8 → 144.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.52 | 1.56 | 10.89 | 99% | 0.46 | 1.23 | 3.33 | 147.5 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.19 | 6.12 | 100% | 0.32 | 0.99 | 3.02 | 68.0 → 67.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 1.78 | 13.37 | 99% | 0.52 | 1.32 | 3.67 | 143.9 → 144.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.80 | 6.55 | 19.22 | 95% | 0.64 | 5.83 | 4.33 | 547.4 → 563.2 | 13 | 3.5 / 31 |

