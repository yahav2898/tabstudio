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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.13 | 3.31 | 14.86 | 99% | 0.62 | 2.79 | 5.08 | 135.9 → 141.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.27 | 3.49 | 20.79 | 99% | 0.79 | 2.79 | 5.13 | 171.1 → 169.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.42 | 4.05 | 17.07 | 98% | 1.03 | 3.09 | 5.49 | 217.9 → 202.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.27 | 5.31 | 85.86 | 99% | 0.88 | 2.90 | 5.30 | 157.6 → 157.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.04 | 3.30 | 13.18 | 99% | 0.72 | 2.57 | 4.95 | 130.7 → 130.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.14 | 3.35 | 17.44 | 99% | 0.76 | 2.73 | 5.12 | 155.5 → 151.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 3.21 | 13.08 | 99% | 0.71 | 2.60 | 4.68 | 158.6 → 153.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 3.62 | 13.95 | 99% | 0.70 | 2.68 | 4.59 | 159.7 → 157.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 3.30 | 16.04 | 99% | 0.65 | 2.68 | 4.46 | 119.9 → 117.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 3.21 | 16.20 | 99% | 0.73 | 2.58 | 4.81 | 160.0 → 156.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.05 | 3.35 | 14.16 | 99% | 0.71 | 2.72 | 4.59 | 160.6 → 155.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 2.74 | 15.76 | 99% | 0.53 | 2.33 | 3.45 | 81.0 → 79.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.340 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
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
| this editor | 368 | 1.17 | 5.13 | 16.44 | 99% | 0.93 | 2.93 | 5.14 | 157.9 → 157.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.16 | 5.54 | 24.32 | 96% | 0.75 | 4.56 | 5.04 | 564.3 → 584.3 | 12 | 3.5 / 31 |

