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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.97 | 2.56 | 14.14 | 99% | 0.57 | 2.07 | 4.45 | 126.6 → 134.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 2.83 | 15.84 | 99% | 0.77 | 2.08 | 4.58 | 163.2 → 160.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 4.19 | 19.21 | 99% | 1.13 | 2.47 | 4.91 | 210.0 → 195.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 3.01 | 13.71 | 99% | 0.77 | 2.26 | 4.67 | 148.9 → 149.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 2.53 | 15.61 | 99% | 0.71 | 1.90 | 4.56 | 123.5 → 121.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 2.67 | 18.20 | 99% | 0.72 | 2.07 | 4.57 | 147.8 → 143.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 2.74 | 16.76 | 99% | 0.76 | 2.10 | 4.65 | 150.0 → 146.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.16 | 4.54 | 17.40 | 99% | 2.54 | 2.63 | 4.84 | 153.6 → 150.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 2.66 | 11.85 | 99% | 0.60 | 2.02 | 4.65 | 113.0 → 110.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.07 | 2.76 | 16.89 | 99% | 0.74 | 2.10 | 4.62 | 152.7 → 149.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 2.66 | 14.58 | 99% | 0.74 | 2.02 | 4.04 | 152.6 → 149.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.23 | 10.00 | 99% | 0.58 | 1.69 | 3.75 | 74.0 → 73.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.323 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.014 |
| MeasuringLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 4.55 | 15.74 | 99% | 0.84 | 2.79 | 4.60 | 149.1 → 150.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.23 | 5.10 | 25.76 | 96% | 0.85 | 4.21 | 4.72 | 557.7 → 574.8 | 12 | 3.5 / 31 |

