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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.03 | 3.28 | 12.81 | 99% | 0.60 | 2.75 | 6.76 | 141.6 → 149.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.32 | 5.25 | 82.31 | 99% | 0.95 | 3.30 | 7.26 | 179.6 → 176.1 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.38 | 3.72 | 16.68 | 99% | 1.06 | 2.86 | 6.89 | 225.5 → 210.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.15 | 3.52 | 14.36 | 99% | 0.80 | 2.90 | 6.79 | 163.3 → 164.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.12 | 3.39 | 10.56 | 100% | 0.82 | 2.79 | 7.09 | 138.0 → 136.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.17 | 3.43 | 15.85 | 99% | 0.78 | 2.79 | 6.83 | 162.7 → 159.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.21 | 3.48 | 24.28 | 99% | 0.79 | 2.87 | 7.11 | 164.8 → 161.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.32 | 3.94 | 17.43 | 99% | 0.99 | 3.08 | 7.46 | 168.2 → 165.4 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.98 | 3.14 | 10.58 | 99% | 0.63 | 2.70 | 6.49 | 126.9 → 124.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 3.15 | 13.56 | 99% | 0.72 | 2.62 | 6.58 | 167.5 → 164.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.21 | 3.64 | 13.43 | 99% | 0.81 | 2.87 | 6.57 | 167.7 → 164.7 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.70 | 2.71 | 9.29 | 99% | 0.57 | 2.35 | 5.48 | 86.7 → 86.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.301 |
| ArrangingLayoutable TextView | 0.020 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 3.06 | 16.71 | 99% | 0.79 | 2.49 | 6.63 | 163.7 → 164.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.31 | 7.91 | 87.06 | 95% | 2.43 | 6.84 | 8.34 | 568.6 → 601.5 | 14 | 3.5 / 31 |

