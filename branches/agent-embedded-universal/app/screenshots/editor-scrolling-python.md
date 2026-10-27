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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.26 | 7.21 | 100% | 0.24 | 1.07 | 2.31 | 126.7 → 133.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 1.39 | 11.15 | 99% | 0.38 | 1.10 | 2.70 | 162.8 → 160.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 1.50 | 9.83 | 99% | 0.50 | 1.12 | 2.80 | 209.7 → 194.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 1.39 | 10.55 | 99% | 0.36 | 1.10 | 2.68 | 148.3 → 149.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.25 | 8.46 | 99% | 0.34 | 1.03 | 2.59 | 123.0 → 121.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.38 | 1.41 | 10.82 | 99% | 0.36 | 1.11 | 2.66 | 147.5 → 144.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.54 | 3.30 | 68.20 | 99% | 2.17 | 2.51 | 2.96 | 150.4 → 147.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.41 | 1.45 | 10.18 | 99% | 0.38 | 1.15 | 2.77 | 153.0 → 149.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.27 | 6.61 | 100% | 0.26 | 1.06 | 2.56 | 112.3 → 110.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.39 | 1.36 | 10.59 | 99% | 0.36 | 1.11 | 2.70 | 152.4 → 149.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.36 | 1.32 | 10.62 | 99% | 0.36 | 1.09 | 2.38 | 152.2 → 148.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.09 | 8.26 | 100% | 0.25 | 0.94 | 2.09 | 73.5 → 73.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.122 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 1.35 | 10.31 | 99% | 0.34 | 1.12 | 2.67 | 148.5 → 149.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.33 | 9.90 | 82.31 | 95% | 2.34 | 5.71 | 3.87 | 559.0 → 572.1 | 14 | 3.5 / 31 |

