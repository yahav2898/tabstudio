# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.33 | 14.75 | 99% | 0.42 | 2.01 | 2.88 | 94.0 → 101.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.52 | 2.37 | 15.31 | 99% | 0.89 | 2.00 | 3.08 | 130.6 → 131.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.55 | 2.41 | 14.94 | 99% | 1.19 | 1.94 | 3.23 | 178.1 → 163.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.46 | 2.32 | 14.31 | 99% | 0.53 | 1.93 | 3.04 | 115.9 → 116.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.48 | 15.55 | 99% | 0.62 | 2.04 | 3.19 | 91.8 → 89.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.63 | 2.56 | 15.50 | 99% | 0.67 | 2.13 | 3.38 | 116.2 → 111.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.66 | 2.86 | 19.53 | 100% | 1.35 | 2.21 | 3.33 | 118.2 → 114.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.70 | 18.51 | 99% | 2.44 | 2.39 | 3.85 | 121.0 → 116.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.57 | 2.46 | 13.27 | 99% | 0.46 | 2.06 | 3.35 | 80.7 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.68 | 2.78 | 16.95 | 99% | 1.24 | 2.24 | 3.83 | 120.7 → 116.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.47 | 14.98 | 99% | 0.57 | 2.09 | 2.80 | 120.8 → 116.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 2.10 | 14.68 | 100% | 0.45 | 1.79 | 2.49 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.249 |
| MeasuringLayoutable Grid | 0.050 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 2.27 | 17.17 | 99% | 0.55 | 1.91 | 3.04 | 117.1 → 118.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.09 | 10.65 | 35.70 | 94% | 2.49 | 7.04 | 6.45 | 588.8 → 605.4 | 14 | 3.5 / 30 |

