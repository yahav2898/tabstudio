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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 2.87 | 13.14 | 99% | 0.66 | 2.32 | 5.75 | 129.7 → 137.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.17 | 3.30 | 18.45 | 99% | 0.91 | 2.42 | 5.85 | 165.9 → 164.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.82 | 5.87 | 22.92 | 99% | 3.14 | 3.17 | 6.93 | 214.0 → 199.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.32 | 3.29 | 20.06 | 99% | 0.89 | 2.54 | 6.09 | 152.3 → 153.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 2.84 | 16.51 | 99% | 0.79 | 2.17 | 5.67 | 126.2 → 124.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.80 | 18.93 | 99% | 0.80 | 2.16 | 5.54 | 150.4 → 146.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.33 | 4.40 | 22.70 | 99% | 1.13 | 2.89 | 6.82 | 154.1 → 150.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.30 | 3.57 | 16.54 | 99% | 0.94 | 2.60 | 6.06 | 156.8 → 153.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 2.83 | 14.89 | 99% | 0.66 | 2.21 | 5.32 | 115.4 → 113.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.35 | 3.45 | 21.48 | 99% | 0.97 | 2.59 | 6.24 | 156.5 → 153.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.25 | 3.59 | 20.28 | 99% | 0.96 | 2.61 | 5.63 | 156.2 → 152.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 2.61 | 6.68 | 100% | 0.61 | 2.06 | 4.68 | 76.9 → 76.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.333 |
| FindingResource - | 0.060 |
| ArrangingLayoutable ContentPresenter | 0.038 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 3.13 | 18.49 | 99% | 0.80 | 2.40 | 5.83 | 151.9 → 153.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.77 | 8.19 | 32.65 | 95% | 0.95 | 7.14 | 6.02 | 559.4 → 579.1 | 14 | 3.5 / 31 |

