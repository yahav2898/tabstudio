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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.12 | 12.05 | 99% | 0.42 | 1.82 | 4.32 | 130.8 → 138.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 2.98 | 15.02 | 99% | 0.72 | 2.23 | 5.21 | 167.4 → 165.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.66 | 16.31 | 99% | 0.89 | 1.99 | 5.27 | 213.5 → 199.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.39 | 13.00 | 99% | 0.63 | 1.90 | 5.17 | 152.5 → 154.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 4.26 | 13.43 | 99% | 0.80 | 2.51 | 5.32 | 127.8 → 126.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.25 | 22.62 | 99% | 0.63 | 1.81 | 5.12 | 151.8 → 148.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.30 | 16.43 | 99% | 0.59 | 1.85 | 4.69 | 154.1 → 150.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.35 | 16.25 | 99% | 0.65 | 1.89 | 5.15 | 156.8 → 153.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.11 | 12.25 | 99% | 0.46 | 1.75 | 4.79 | 116.7 → 115.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 4.70 | 19.00 | 99% | 2.53 | 3.88 | 5.46 | 157.5 → 154.9 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.25 | 25.64 | 99% | 0.62 | 1.83 | 4.36 | 156.7 → 153.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.81 | 13.71 | 99% | 0.45 | 1.53 | 4.06 | 77.7 → 77.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.196 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 2.49 | 14.46 | 99% | 0.63 | 1.93 | 4.88 | 152.7 → 153.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.22 | 8.71 | 28.19 | 95% | 2.67 | 6.82 | 6.35 | 557.8 → 575.6 | 14 | 3.5 / 31 |

