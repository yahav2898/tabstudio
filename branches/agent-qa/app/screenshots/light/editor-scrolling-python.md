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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.50 | 14.78 | 99% | 0.45 | 2.10 | 3.18 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.82 | 17.39 | 99% | 0.91 | 2.23 | 3.51 | 131.3 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.81 | 15.57 | 99% | 1.34 | 2.16 | 3.50 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 2.82 | 14.72 | 99% | 1.18 | 2.17 | 2.98 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.82 | 10.61 | 99% | 1.32 | 2.08 | 3.18 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.45 | 2.31 | 5.85 | 100% | 0.61 | 1.97 | 3.37 | 116.4 → 112.4 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 2.36 | 15.24 | 99% | 0.91 | 2.02 | 3.18 | 118.6 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.69 | 16.23 | 99% | 0.59 | 2.17 | 3.30 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 2.36 | 10.93 | 100% | 0.44 | 2.02 | 2.82 | 81.3 → 79.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.53 | 2.74 | 14.75 | 99% | 1.21 | 2.17 | 3.14 | 121.2 → 118.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 3.25 | 72.78 | 99% | 1.30 | 2.42 | 2.98 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 2.00 | 8.37 | 100% | 0.30 | 1.78 | 2.35 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.45 | 2.25 | 13.73 | 99% | 0.46 | 1.96 | 2.89 | 117.2 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.59 | 4.74 | 22.81 | 96% | 1.05 | 3.85 | 5.65 | 583.7 → 600.8 | 13 | 3.5 / 31 |

