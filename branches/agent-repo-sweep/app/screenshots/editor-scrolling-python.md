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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.06 | 15.02 | 100% | 0.46 | 1.74 | 4.61 | 95.5 → 102.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.42 | 15.51 | 99% | 1.13 | 1.77 | 4.76 | 131.7 → 129.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 5.00 | 81.29 | 98% | 2.83 | 2.60 | 5.85 | 178.7 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.18 | 15.19 | 99% | 0.69 | 1.67 | 5.01 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.00 | 13.81 | 99% | 0.67 | 1.52 | 4.91 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.12 | 18.67 | 99% | 0.77 | 1.60 | 4.72 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.98 | 16.45 | 99% | 1.34 | 1.88 | 4.87 | 118.9 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.55 | 17.19 | 99% | 1.36 | 1.77 | 5.12 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 1.89 | 12.09 | 99% | 0.51 | 1.55 | 4.91 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.26 | 16.31 | 99% | 0.74 | 1.70 | 4.92 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 2.41 | 16.06 | 99% | 0.86 | 1.77 | 4.75 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.59 | 20.77 | 100% | 1.45 | 1.52 | 4.47 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| AttachingStyle - | 0.086 |
| ArrangingLayoutable Border | 0.054 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 2.28 | 17.62 | 99% | 0.77 | 1.73 | 5.01 | 117.5 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.57 | 8.09 | 24.96 | 94% | 1.61 | 6.57 | 9.36 | 615.7 → 636.9 | 15 | 3.5 / 31 |

