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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.88 | 3.39 | 12.43 | 99% | 0.55 | 2.92 | 5.00 | 130.1 → 138.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.20 | 4.66 | 15.34 | 99% | 0.94 | 3.34 | 5.65 | 166.6 → 165.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 3.91 | 16.56 | 99% | 1.09 | 2.98 | 5.88 | 213.6 → 198.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.17 | 3.71 | 14.11 | 99% | 0.84 | 2.96 | 5.48 | 152.6 → 153.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 3.34 | 11.71 | 99% | 0.78 | 2.67 | 5.50 | 126.0 → 124.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.10 | 4.48 | 13.17 | 99% | 1.23 | 3.04 | 5.73 | 152.3 → 147.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.03 | 3.40 | 13.78 | 99% | 0.77 | 2.79 | 5.86 | 153.8 → 149.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 3.10 | 22.39 | 99% | 0.72 | 2.61 | 5.26 | 155.2 → 152.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.07 | 15.84 | 99% | 0.57 | 2.59 | 4.81 | 114.7 → 113.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 3.28 | 11.87 | 99% | 0.71 | 2.63 | 5.23 | 155.4 → 152.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 3.04 | 14.92 | 99% | 0.70 | 2.57 | 4.47 | 154.8 → 151.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.58 | 10.88 | 100% | 0.55 | 2.28 | 4.29 | 76.9 → 76.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.310 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.27 | 3.86 | 15.37 | 99% | 1.02 | 3.15 | 6.09 | 152.9 → 153.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.61 | 6.53 | 26.26 | 96% | 0.93 | 5.39 | 5.78 | 556.8 → 577.3 | 13 | 3.5 / 31 |

