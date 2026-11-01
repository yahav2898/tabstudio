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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.45 | 16.67 | 99% | 0.46 | 2.06 | 3.05 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.79 | 17.72 | 100% | 1.28 | 2.22 | 3.40 | 129.7 → 130.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 2.88 | 19.04 | 99% | 1.40 | 2.22 | 3.58 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.64 | 15.71 | 99% | 1.09 | 2.15 | 3.42 | 115.1 → 116.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.41 | 13.47 | 99% | 0.56 | 2.01 | 3.00 | 91.5 → 89.2 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.81 | 2.89 | 18.59 | 99% | 0.79 | 2.24 | 3.94 | 115.4 → 110.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.78 | 3.92 | 21.78 | 99% | 1.44 | 2.43 | 3.55 | 117.4 → 113.3 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.55 | 2.87 | 98.60 | 99% | 1.40 | 2.34 | 3.20 | 120.2 → 116.1 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.56 | 2.37 | 11.93 | 100% | 0.48 | 1.99 | 2.97 | 80.7 → 78.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 2.84 | 17.59 | 99% | 1.24 | 2.24 | 3.73 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 2.78 | 18.51 | 99% | 1.21 | 2.25 | 3.49 | 120.0 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.35 | 13.63 | 100% | 0.58 | 1.95 | 3.06 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| MeasuringLayoutable Border | 0.057 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 3.39 | 43.03 | 99% | 2.42 | 2.34 | 3.74 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.79 | 5.09 | 28.52 | 96% | 1.05 | 4.43 | 6.01 | 576.6 → 599.4 | 13 | 3.5 / 30 |

