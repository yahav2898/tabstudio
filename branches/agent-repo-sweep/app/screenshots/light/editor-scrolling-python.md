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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.13 | 13.09 | 100% | 0.48 | 2.72 | 4.79 | 96.1 → 103.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 4.57 | 17.62 | 99% | 1.87 | 2.96 | 5.25 | 132.6 → 130.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 3.78 | 17.37 | 99% | 1.51 | 2.88 | 5.57 | 179.9 → 165.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 3.30 | 15.75 | 99% | 0.77 | 2.73 | 5.07 | 117.7 → 119.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 3.11 | 18.52 | 99% | 0.77 | 2.57 | 5.00 | 92.6 → 90.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 4.01 | 88.92 | 99% | 1.45 | 2.86 | 5.27 | 117.6 → 113.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 3.20 | 17.81 | 99% | 0.82 | 2.73 | 4.95 | 119.7 → 116.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.37 | 15.58 | 99% | 0.83 | 2.79 | 4.93 | 122.6 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.25 | 13.44 | 99% | 0.63 | 2.79 | 4.72 | 81.7 → 79.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.94 | 3.66 | 20.38 | 99% | 1.41 | 2.93 | 5.77 | 122.4 → 119.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 3.42 | 15.87 | 99% | 0.83 | 2.83 | 4.55 | 122.4 → 118.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.84 | 3.87 | 100% | 0.47 | 2.46 | 4.02 | 43.4 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.273 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 4.95 | 17.61 | 99% | 1.63 | 3.00 | 5.25 | 118.4 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.13 | 7.18 | 25.21 | 95% | 0.96 | 6.35 | 9.40 | 619.1 → 638.4 | 15 | 3.5 / 31 |

