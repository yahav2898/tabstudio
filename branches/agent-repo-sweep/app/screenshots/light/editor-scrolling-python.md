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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.89 | 19.33 | 100% | 0.59 | 2.45 | 5.57 | 95.3 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.08 | 3.64 | 21.85 | 100% | 1.56 | 2.56 | 6.45 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 3.51 | 21.92 | 99% | 1.44 | 2.55 | 6.53 | 177.9 → 163.3 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 3.17 | 20.16 | 99% | 0.87 | 2.52 | 6.18 | 116.6 → 118.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 3.98 | 8.19 | 100% | 1.44 | 2.40 | 6.37 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 3.19 | 22.09 | 99% | 0.87 | 2.46 | 5.96 | 116.5 → 112.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.96 | 3.21 | 20.90 | 99% | 0.90 | 2.48 | 6.46 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 4.28 | 14.72 | 100% | 2.45 | 2.60 | 6.48 | 121.5 → 118.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 2.90 | 20.28 | 100% | 0.62 | 2.36 | 5.97 | 81.3 → 79.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 3.18 | 20.25 | 99% | 0.85 | 2.51 | 6.14 | 121.2 → 118.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.93 | 3.86 | 71.10 | 99% | 1.40 | 2.57 | 5.52 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.59 | 4.42 | 100% | 0.57 | 2.10 | 4.95 | 43.3 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.351 |
| MeasuringLayoutable LineDrawing | 0.078 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.95 | 3.09 | 21.00 | 99% | 0.84 | 2.44 | 5.57 | 117.2 → 118.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.50 | 10.48 | 31.66 | 94% | 1.56 | 8.56 | 11.16 | 620.6 → 632.9 | 9 | 3.5 / 31 |

