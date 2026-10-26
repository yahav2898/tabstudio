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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 1.49 | 8.81 | 99% | 0.34 | 1.26 | 3.35 | 123.8 → 131.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 1.69 | 12.02 | 99% | 0.50 | 1.32 | 3.72 | 160.6 → 158.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.93 | 73.71 | 99% | 2.52 | 2.69 | 4.20 | 208.3 → 193.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.71 | 12.06 | 99% | 0.49 | 1.32 | 3.72 | 145.8 → 147.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 1.54 | 9.44 | 99% | 0.49 | 1.22 | 3.68 | 120.1 → 118.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 1.65 | 11.09 | 99% | 0.49 | 1.28 | 3.68 | 145.5 → 141.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.54 | 1.63 | 12.39 | 99% | 0.48 | 1.29 | 3.64 | 147.3 → 144.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.14 | 11.66 | 99% | 0.57 | 1.43 | 3.81 | 150.5 → 147.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 2.79 | 14.53 | 100% | 0.49 | 2.28 | 3.91 | 109.9 → 108.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 1.65 | 12.32 | 99% | 0.49 | 1.31 | 3.72 | 149.9 → 146.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 1.65 | 12.99 | 99% | 0.51 | 1.31 | 3.35 | 149.9 → 146.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.26 | 5.94 | 100% | 0.35 | 1.03 | 3.09 | 70.1 → 69.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.160 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.56 | 1.67 | 12.11 | 99% | 0.51 | 1.33 | 3.79 | 146.0 → 147.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.03 | 10.80 | 79.77 | 95% | 2.34 | 6.16 | 4.27 | 552.1 → 565.6 | 13 | 3.5 / 31 |

