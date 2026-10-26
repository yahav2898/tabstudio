# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 1.19 | 9.06 | 99% | 0.24 | 1.06 | 2.38 | 120.9 → 128.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.42 | 1.38 | 11.66 | 99% | 0.38 | 1.09 | 2.78 | 157.9 → 155.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.48 | 1.44 | 12.24 | 99% | 0.51 | 1.09 | 2.87 | 205.2 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.38 | 1.31 | 11.18 | 99% | 0.37 | 1.09 | 2.72 | 143.0 → 144.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.32 | 10.95 | 99% | 0.38 | 1.03 | 2.74 | 117.6 → 115.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 3.36 | 67.17 | 99% | 0.60 | 2.36 | 2.97 | 143.0 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.35 | 11.42 | 99% | 0.36 | 1.09 | 2.81 | 144.8 → 141.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.38 | 1.33 | 10.79 | 99% | 0.37 | 1.10 | 2.72 | 147.5 → 144.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.32 | 1.21 | 7.78 | 100% | 0.27 | 1.04 | 2.52 | 106.7 → 104.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 1.31 | 10.68 | 99% | 0.36 | 1.09 | 2.79 | 147.3 → 144.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 1.33 | 10.67 | 99% | 0.36 | 1.09 | 2.45 | 147.4 → 144.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.06 | 7.10 | 100% | 0.24 | 0.91 | 2.29 | 67.9 → 67.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable ScrollingTextLayer | 0.027 |
| ArrangingLayoutable ScrollContentPresenter | 0.018 |
| ArrangingLayoutable CachedLineNumberMargin | 0.017 |
| MeasuringLayoutable LineDrawing | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.45 | 3.16 | 10.83 | 99% | 0.61 | 2.32 | 2.93 | 144.2 → 144.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 3.34 | 18.08 | 96% | 0.44 | 2.70 | 3.08 | 550.7 → 570.6 | 13 | 3.5 / 31 |

