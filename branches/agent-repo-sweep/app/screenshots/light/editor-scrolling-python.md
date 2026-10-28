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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.32 | 1.93 | 7.74 | 100% | 0.26 | 1.75 | 2.35 | 95.3 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.42 | 2.08 | 13.21 | 99% | 0.77 | 1.79 | 2.81 | 131.3 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.45 | 2.17 | 13.42 | 99% | 0.88 | 1.78 | 2.87 | 177.9 → 163.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.51 | 2.21 | 13.33 | 99% | 0.60 | 1.88 | 2.82 | 116.5 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.61 | 13.16 | 99% | 1.15 | 2.20 | 3.29 | 91.9 → 90.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.40 | 2.81 | 68.27 | 99% | 0.99 | 1.98 | 2.85 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 1.98 | 13.82 | 99% | 0.47 | 1.78 | 2.83 | 118.6 → 115.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.40 | 2.03 | 14.60 | 99% | 0.64 | 1.77 | 2.86 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.32 | 2.07 | 7.55 | 100% | 0.35 | 1.77 | 2.73 | 81.2 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.48 | 2.27 | 13.59 | 99% | 0.47 | 1.88 | 2.94 | 121.1 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 2.00 | 12.11 | 99% | 0.60 | 1.76 | 2.46 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 2.31 | 12.26 | 100% | 1.06 | 1.77 | 2.43 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.225 |
| AttachingStyle - | 0.092 |
| FindingResource - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
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
| this editor | 368 | 0.56 | 2.40 | 12.99 | 99% | 0.53 | 2.01 | 3.11 | 117.2 → 117.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.19 | 4.46 | 19.64 | 96% | 0.99 | 3.50 | 5.04 | 572.7 → 594.6 | 13 | 3.5 / 31 |

