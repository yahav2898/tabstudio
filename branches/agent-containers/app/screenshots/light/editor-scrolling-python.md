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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 4.97 | 14.87 | 99% | 0.65 | 3.11 | 6.66 | 134.7 → 142.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 3.38 | 16.18 | 99% | 0.65 | 2.82 | 6.21 | 171.0 → 169.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.53 | 17.65 | 99% | 0.94 | 2.81 | 6.01 | 217.5 → 201.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.07 | 5.57 | 76.84 | 99% | 0.81 | 3.43 | 7.03 | 157.3 → 159.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.04 | 13.03 | 99% | 0.57 | 2.64 | 5.86 | 130.7 → 129.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 3.20 | 23.43 | 99% | 0.61 | 2.76 | 6.04 | 155.6 → 152.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.65 | 15.99 | 99% | 0.66 | 3.09 | 6.40 | 157.3 → 154.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.63 | 15.07 | 99% | 0.62 | 3.15 | 6.85 | 160.9 → 157.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 3.05 | 11.46 | 99% | 0.45 | 2.69 | 5.63 | 120.9 → 118.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.37 | 15.64 | 99% | 0.64 | 2.83 | 6.21 | 160.5 → 158.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.92 | 4.14 | 78.18 | 99% | 0.74 | 3.32 | 6.37 | 161.2 → 157.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.88 | 10.52 | 99% | 0.43 | 2.53 | 4.95 | 81.3 → 81.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.199 |
| ArrangingLayoutable LineDrawing | 0.048 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.57 | 17.08 | 99% | 0.64 | 2.99 | 6.31 | 156.3 → 158.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.45 | 7.32 | 26.42 | 95% | 0.77 | 6.34 | 7.42 | 563.7 → 582.4 | 13 | 3.5 / 31 |

