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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.64 | 15.21 | 99% | 0.49 | 2.21 | 3.05 | 94.0 → 101.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.85 | 52.10 | 99% | 1.14 | 2.26 | 3.55 | 130.6 → 131.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.00 | 16.76 | 99% | 1.57 | 2.26 | 3.78 | 178.0 → 163.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 2.77 | 15.08 | 99% | 0.65 | 2.17 | 3.48 | 115.9 → 116.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.69 | 14.90 | 99% | 1.47 | 2.12 | 3.84 | 91.8 → 89.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.90 | 3.16 | 27.73 | 99% | 1.65 | 2.37 | 3.71 | 116.2 → 111.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.94 | 3.60 | 88.29 | 98% | 1.76 | 2.42 | 3.91 | 118.2 → 114.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.88 | 17.09 | 99% | 1.38 | 2.31 | 3.79 | 121.0 → 116.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.71 | 2.63 | 13.88 | 100% | 0.51 | 2.16 | 2.95 | 80.7 → 78.4 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.86 | 2.89 | 17.35 | 99% | 0.74 | 2.29 | 3.70 | 120.7 → 116.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.85 | 2.93 | 14.63 | 99% | 0.96 | 2.33 | 3.38 | 120.8 → 116.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.52 | 13.30 | 100% | 1.14 | 2.00 | 2.87 | 43.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.437 |
| ArrangingLayoutable Thumb | 0.068 |
| AttachingStyle - | 0.049 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable Track | 0.019 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.013 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.81 | 36.39 | 99% | 0.70 | 2.26 | 3.49 | 117.1 → 118.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 5.96 | 28.15 | 96% | 0.74 | 4.75 | 6.36 | 574.8 → 601.3 | 13 | 3.5 / 30 |

