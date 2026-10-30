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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.88 | 16.54 | 99% | 0.44 | 2.62 | 4.23 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 3.28 | 16.96 | 99% | 0.87 | 2.68 | 5.17 | 131.7 → 129.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.38 | 5.71 | 90.75 | 99% | 2.78 | 3.53 | 6.24 | 178.7 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.52 | 17.51 | 99% | 0.81 | 2.80 | 5.57 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.93 | 15.42 | 99% | 0.68 | 2.50 | 4.94 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 3.06 | 15.73 | 99% | 0.73 | 2.63 | 4.66 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.92 | 4.81 | 19.93 | 99% | 1.74 | 3.06 | 5.90 | 119.0 → 115.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.49 | 15.88 | 100% | 0.90 | 2.86 | 5.31 | 121.9 → 118.3 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 3.02 | 14.23 | 99% | 0.57 | 2.57 | 4.78 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 3.31 | 17.78 | 99% | 0.78 | 2.80 | 5.24 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.98 | 4.92 | 20.65 | 99% | 2.38 | 3.01 | 5.25 | 121.8 → 118.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.71 | 8.32 | 100% | 0.49 | 2.40 | 4.06 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.221 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.10 | 17.85 | 99% | 0.68 | 2.66 | 4.93 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.46 | 10.31 | 31.91 | 92% | 2.45 | 8.46 | 9.64 | 620.1 → 645.7 | 15 | 3.5 / 31 |

