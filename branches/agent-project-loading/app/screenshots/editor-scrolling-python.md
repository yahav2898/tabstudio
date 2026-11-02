# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.76 | 13.95 | 99% | 0.47 | 2.35 | 3.68 | 94.3 → 101.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 3.01 | 17.66 | 99% | 1.51 | 2.44 | 3.83 | 131.0 → 131.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.34 | 18.34 | 99% | 1.83 | 2.52 | 4.69 | 178.8 → 164.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.99 | 17.27 | 99% | 0.73 | 2.47 | 4.20 | 116.3 → 117.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.16 | 16.72 | 99% | 1.79 | 2.42 | 4.40 | 92.2 → 89.9 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.81 | 3.12 | 24.72 | 99% | 1.72 | 2.49 | 4.59 | 116.7 → 111.9 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.79 | 3.00 | 17.44 | 99% | 1.39 | 2.41 | 3.96 | 118.6 → 114.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.36 | 19.11 | 99% | 1.08 | 2.61 | 4.61 | 121.4 → 117.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.70 | 2.90 | 14.12 | 99% | 0.64 | 2.39 | 4.33 | 81.2 → 78.8 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.71 | 2.83 | 17.23 | 99% | 0.87 | 2.39 | 4.18 | 121.1 → 117.3 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.80 | 4.31 | 19.10 | 99% | 1.96 | 2.63 | 3.94 | 121.2 → 117.1 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.92 | 9.36 | 100% | 1.64 | 2.27 | 3.46 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.300 |
| AttachingStyle - | 0.079 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.07 | 26.45 | 99% | 0.68 | 2.47 | 4.26 | 117.5 → 118.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.41 | 10.12 | 101.85 | 93% | 2.11 | 7.59 | 8.18 | 606.2 → 639.4 | 15 | 3.5 / 30 |

