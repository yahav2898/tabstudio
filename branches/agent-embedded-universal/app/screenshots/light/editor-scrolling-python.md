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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.89 | 13.07 | 99% | 0.49 | 2.54 | 4.92 | 128.8 → 136.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 4.43 | 17.82 | 99% | 0.77 | 3.01 | 5.28 | 165.7 → 164.1 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 3.15 | 15.84 | 99% | 0.89 | 2.60 | 5.15 | 211.6 → 196.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.88 | 15.04 | 99% | 0.61 | 2.37 | 4.84 | 150.4 → 151.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.71 | 14.96 | 99% | 0.57 | 2.33 | 4.82 | 125.2 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 3.23 | 17.94 | 99% | 0.66 | 2.66 | 4.94 | 149.7 → 146.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 3.47 | 84.24 | 99% | 0.74 | 2.91 | 5.38 | 152.8 → 149.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.03 | 3.45 | 16.75 | 99% | 0.74 | 2.77 | 5.79 | 155.7 → 152.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.85 | 3.07 | 10.62 | 99% | 0.58 | 2.65 | 5.08 | 115.1 → 113.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.24 | 4.03 | 19.28 | 99% | 1.11 | 3.06 | 5.66 | 156.0 → 153.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 3.35 | 17.20 | 99% | 0.73 | 2.80 | 4.74 | 154.9 → 151.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.70 | 12.19 | 99% | 0.53 | 2.36 | 4.31 | 76.5 → 76.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| ArrangingLayoutable TextView | 0.060 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.32 | 20.66 | 99% | 0.71 | 2.67 | 5.11 | 150.6 → 152.5 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.38 | 5.72 | 32.41 | 96% | 0.75 | 5.07 | 5.20 | 552.6 → 566.7 | 12 | 3.5 / 31 |

