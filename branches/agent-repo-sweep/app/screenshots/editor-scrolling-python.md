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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.94 | 12.11 | 99% | 0.53 | 2.55 | 4.20 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.13 | 17.86 | 99% | 0.86 | 2.55 | 4.38 | 131.7 → 129.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.28 | 18.00 | 99% | 1.72 | 2.54 | 4.81 | 178.6 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.11 | 15.71 | 99% | 1.61 | 2.49 | 4.60 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 3.22 | 19.67 | 99% | 1.68 | 2.47 | 4.87 | 92.3 → 90.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 3.01 | 20.12 | 99% | 0.74 | 2.54 | 4.85 | 116.8 → 112.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 3.42 | 26.15 | 99% | 0.73 | 2.74 | 4.80 | 118.9 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.67 | 19.24 | 99% | 0.85 | 2.83 | 5.53 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 3.58 | 14.07 | 100% | 1.73 | 2.86 | 4.89 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.90 | 3.35 | 16.33 | 99% | 0.90 | 2.76 | 4.78 | 121.6 → 118.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.35 | 16.72 | 99% | 0.79 | 2.69 | 4.08 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.75 | 14.62 | 100% | 0.60 | 2.32 | 3.67 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.243 |
| FindingResource - | 0.062 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.08 | 15.40 | 99% | 0.66 | 2.51 | 4.40 | 117.6 → 118.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.01 | 9.31 | 36.06 | 95% | 1.83 | 7.60 | 8.85 | 618.4 → 635.9 | 13 | 3.5 / 31 |

