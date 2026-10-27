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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.21 | 5.43 | 94.92 | 99% | 0.78 | 4.54 | 7.55 | 136.8 → 145.2 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.28 | 3.61 | 16.97 | 99% | 0.84 | 2.94 | 6.94 | 173.1 → 171.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.41 | 3.85 | 19.08 | 99% | 1.12 | 2.99 | 7.15 | 220.4 → 204.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.28 | 3.74 | 18.52 | 99% | 0.84 | 3.00 | 7.41 | 158.5 → 160.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.04 | 3.25 | 17.60 | 99% | 0.80 | 2.69 | 6.95 | 132.9 → 130.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.24 | 3.48 | 18.19 | 99% | 0.84 | 2.84 | 7.05 | 157.9 → 153.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.17 | 3.42 | 25.49 | 99% | 0.80 | 2.80 | 7.13 | 159.8 → 156.2 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.34 | 4.51 | 14.35 | 99% | 0.90 | 3.10 | 7.71 | 163.5 → 159.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.09 | 3.02 | 11.94 | 99% | 0.68 | 2.60 | 6.66 | 121.5 → 120.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.29 | 3.43 | 17.14 | 99% | 0.85 | 2.86 | 7.23 | 162.0 → 159.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.27 | 3.79 | 18.11 | 99% | 0.86 | 3.07 | 6.58 | 161.9 → 159.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.87 | 2.70 | 13.38 | 99% | 0.63 | 2.26 | 5.96 | 83.7 → 81.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.380 |
| ArrangingLayoutable CachedLineNumberMargin | 0.061 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.36 | 3.70 | 19.20 | 99% | 0.85 | 2.93 | 7.44 | 158.6 → 159.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.46 | 9.69 | 103.97 | 94% | 2.46 | 7.62 | 8.81 | 576.2 → 593.4 | 14 | 3.5 / 31 |

