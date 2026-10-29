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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.37 | 2.53 | 8.96 | 99% | 0.56 | 1.96 | 2.67 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 2.18 | 14.15 | 99% | 0.77 | 1.87 | 2.94 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.57 | 2.39 | 14.50 | 99% | 1.11 | 1.95 | 3.10 | 177.9 → 163.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 2.17 | 13.08 | 99% | 0.82 | 1.90 | 2.95 | 116.6 → 118.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 2.04 | 12.06 | 99% | 0.63 | 1.78 | 2.86 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.38 | 2.16 | 14.86 | 99% | 0.48 | 1.88 | 2.86 | 116.4 → 112.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 3.84 | 15.38 | 99% | 1.45 | 2.48 | 3.46 | 118.5 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 2.31 | 17.46 | 99% | 0.50 | 1.95 | 2.97 | 121.4 → 117.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.35 | 2.05 | 13.30 | 100% | 0.35 | 1.82 | 2.66 | 81.2 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 2.13 | 15.98 | 99% | 0.49 | 1.87 | 2.90 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.42 | 2.22 | 13.63 | 99% | 0.42 | 1.89 | 2.52 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.09 | 6.55 | 100% | 0.49 | 1.81 | 2.67 | 43.3 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.275 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 4.02 | 14.04 | 99% | 1.27 | 2.34 | 3.28 | 117.2 → 118.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.35 | 4.64 | 22.31 | 96% | 0.59 | 3.94 | 5.18 | 580.0 → 599.5 | 14 | 3.5 / 31 |

