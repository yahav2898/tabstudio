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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.52 | 13.05 | 100% | 0.47 | 2.12 | 3.08 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 2.68 | 16.79 | 99% | 0.96 | 2.12 | 3.17 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.91 | 18.37 | 99% | 1.55 | 2.19 | 3.71 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.61 | 16.74 | 99% | 0.51 | 2.16 | 3.11 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.56 | 17.63 | 99% | 0.60 | 2.08 | 3.33 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.63 | 2.76 | 18.10 | 99% | 0.84 | 2.23 | 3.25 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.86 | 4.22 | 20.13 | 99% | 2.38 | 2.59 | 3.71 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.74 | 13.99 | 99% | 0.98 | 2.20 | 3.88 | 119.8 → 115.8 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.57 | 2.70 | 20.44 | 99% | 0.56 | 2.25 | 3.47 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.57 | 2.80 | 16.24 | 99% | 0.75 | 2.22 | 3.37 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.67 | 2.81 | 17.83 | 99% | 0.68 | 2.26 | 2.87 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.34 | 12.18 | 100% | 0.61 | 1.95 | 2.84 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.264 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 4.94 | 38.56 | 98% | 2.59 | 2.84 | 5.00 | 116.0 → 117.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.93 | 5.69 | 28.16 | 96% | 0.81 | 4.49 | 6.30 | 572.3 → 599.5 | 13 | 3.5 / 30 |

