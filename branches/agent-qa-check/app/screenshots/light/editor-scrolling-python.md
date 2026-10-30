# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.25 | 15.71 | 99% | 0.52 | 2.77 | 4.67 | 96.1 → 103.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.93 | 19.47 | 99% | 1.51 | 2.93 | 5.38 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.72 | 18.02 | 98% | 1.58 | 2.79 | 5.38 | 179.9 → 165.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 3.18 | 21.42 | 99% | 0.73 | 2.71 | 4.99 | 117.7 → 119.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.11 | 17.21 | 99% | 0.69 | 2.60 | 4.96 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 4.58 | 19.76 | 99% | 2.53 | 2.86 | 5.61 | 117.6 → 113.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 3.63 | 17.11 | 99% | 1.06 | 2.94 | 6.59 | 119.8 → 116.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.35 | 16.92 | 99% | 0.76 | 2.74 | 5.04 | 122.7 → 119.1 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 3.09 | 11.45 | 99% | 0.49 | 2.68 | 4.86 | 81.8 → 79.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.32 | 19.36 | 99% | 2.22 | 2.88 | 5.32 | 122.4 → 119.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 3.17 | 16.62 | 99% | 0.69 | 2.68 | 4.51 | 122.5 → 118.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.73 | 4.03 | 100% | 0.47 | 2.39 | 4.15 | 43.4 → 42.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| AttachingStyle - | 0.079 |
| ArrangingLayoutable TextView | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.52 | 20.56 | 99% | 1.22 | 2.85 | 5.14 | 118.3 → 119.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.26 | 8.82 | 36.17 | 93% | 1.53 | 8.14 | 9.31 | 622.1 → 638.5 | 14 | 3.5 / 31 |

