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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.10 | 5.58 | 12.39 | 100% | 0.63 | 4.88 | 7.30 | 137.0 → 143.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.60 | 15.92 | 99% | 0.70 | 3.02 | 6.41 | 172.5 → 170.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 3.87 | 16.37 | 99% | 1.00 | 3.05 | 6.47 | 221.0 → 205.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.25 | 5.66 | 18.22 | 99% | 0.86 | 3.67 | 7.23 | 158.2 → 161.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.95 | 3.92 | 13.09 | 99% | 0.73 | 3.23 | 6.98 | 132.6 → 131.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.96 | 17.70 | 99% | 0.82 | 3.27 | 7.03 | 157.7 → 153.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.16 | 4.07 | 20.85 | 99% | 0.82 | 3.31 | 7.16 | 159.8 → 156.8 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.02 | 3.94 | 17.30 | 99% | 0.73 | 3.22 | 6.80 | 162.9 → 158.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 3.47 | 11.21 | 99% | 0.55 | 2.94 | 6.16 | 122.1 → 119.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 3.40 | 14.54 | 99% | 0.68 | 2.89 | 6.23 | 161.7 → 158.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.12 | 4.95 | 88.46 | 99% | 0.82 | 3.34 | 6.03 | 163.4 → 159.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.96 | 12.88 | 100% | 0.46 | 2.59 | 5.01 | 82.1 → 81.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| AttachingStyle - | 0.049 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.42 | 16.26 | 99% | 0.66 | 2.94 | 6.19 | 157.9 → 158.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.24 | 8.90 | 29.24 | 93% | 2.55 | 7.17 | 7.42 | 570.7 → 591.6 | 14 | 3.5 / 31 |

