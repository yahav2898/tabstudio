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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.96 | 82.01 | 99% | 1.11 | 2.93 | 5.09 | 96.2 → 103.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.05 | 16.70 | 99% | 0.82 | 2.58 | 4.65 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 3.43 | 22.85 | 99% | 1.26 | 2.64 | 5.09 | 179.9 → 165.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.98 | 14.21 | 99% | 0.61 | 2.57 | 4.71 | 117.7 → 119.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 4.75 | 14.67 | 99% | 1.49 | 2.76 | 5.16 | 92.6 → 90.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 2.97 | 18.05 | 99% | 0.67 | 2.52 | 4.58 | 117.5 → 113.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.61 | 3.06 | 22.94 | 99% | 0.84 | 2.59 | 5.01 | 119.7 → 116.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.99 | 18.11 | 99% | 0.68 | 2.59 | 4.96 | 122.6 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 4.47 | 7.45 | 100% | 1.36 | 2.91 | 5.29 | 81.8 → 79.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 3.01 | 15.45 | 99% | 1.07 | 2.60 | 5.13 | 122.3 → 119.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.88 | 17.17 | 99% | 0.69 | 2.51 | 4.24 | 122.5 → 118.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 2.53 | 3.64 | 100% | 0.44 | 2.26 | 3.95 | 43.4 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.268 |
| MeasuringLayoutable ContentPresenter | 0.041 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 4.11 | 16.54 | 99% | 1.33 | 2.90 | 5.22 | 118.4 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.51 | 6.72 | 26.00 | 96% | 1.44 | 5.47 | 8.65 | 612.9 → 636.4 | 14 | 3.5 / 31 |

