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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.64 | 12.66 | 100% | 0.49 | 2.19 | 3.31 | 94.0 → 101.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.84 | 18.83 | 99% | 1.30 | 2.25 | 3.51 | 130.6 → 131.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.00 | 20.02 | 99% | 1.58 | 2.36 | 3.97 | 178.1 → 163.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.71 | 27.86 | 99% | 0.76 | 2.22 | 3.95 | 115.9 → 116.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.52 | 16.47 | 100% | 0.66 | 2.04 | 3.55 | 91.9 → 89.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 3.13 | 22.29 | 99% | 1.65 | 2.31 | 3.90 | 116.2 → 111.5 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.78 | 3.76 | 94.94 | 99% | 1.48 | 2.55 | 3.86 | 118.2 → 114.1 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.90 | 17.30 | 99% | 1.34 | 2.31 | 3.74 | 121.0 → 116.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.69 | 2.66 | 15.19 | 99% | 0.51 | 2.18 | 3.11 | 80.8 → 78.5 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.84 | 2.93 | 17.00 | 99% | 0.72 | 2.32 | 3.68 | 120.7 → 116.9 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 2.76 | 20.62 | 99% | 0.70 | 2.30 | 3.14 | 120.9 → 116.8 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.37 | 3.80 | 100% | 0.49 | 1.97 | 2.91 | 43.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.367 |
| ArrangingLayoutable CachedLineNumberMargin | 0.063 |
| AttachingStyle - | 0.040 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.015 |
| MeasuringLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.69 | 35.75 | 99% | 0.61 | 2.17 | 3.69 | 117.1 → 118.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 6.22 | 26.87 | 96% | 1.41 | 4.69 | 6.51 | 579.4 → 602.6 | 14 | 3.5 / 30 |

