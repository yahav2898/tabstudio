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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.83 | 15.88 | 100% | 0.45 | 2.43 | 3.90 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.90 | 16.77 | 99% | 0.88 | 2.36 | 3.85 | 131.2 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 5.22 | 78.83 | 99% | 2.57 | 2.69 | 4.72 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.91 | 14.72 | 99% | 0.62 | 2.46 | 4.28 | 116.5 → 117.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.41 | 15.08 | 99% | 0.55 | 2.09 | 3.95 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 2.63 | 16.24 | 99% | 0.62 | 2.26 | 3.93 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 2.72 | 16.98 | 99% | 0.71 | 2.29 | 4.20 | 118.5 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 4.88 | 15.65 | 99% | 2.41 | 2.66 | 4.36 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.69 | 12.84 | 100% | 0.47 | 2.31 | 4.07 | 81.2 → 79.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 2.91 | 13.44 | 99% | 0.57 | 2.41 | 4.15 | 121.1 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 2.74 | 25.68 | 100% | 0.55 | 2.27 | 3.75 | 121.3 → 117.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.44 | 14.39 | 100% | 0.40 | 2.12 | 3.47 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.321 |
| ArrangingLayoutable ScrollingTextLayer | 0.038 |
| AttachingStyle - | 0.023 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable Track | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.13 | 13.49 | 99% | 1.66 | 2.51 | 4.19 | 117.2 → 117.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.82 | 7.26 | 24.44 | 95% | 1.54 | 4.84 | 7.23 | 588.2 → 611.9 | 14 | 3.5 / 31 |

