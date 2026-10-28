# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.88 | 14.26 | 99% | 0.41 | 1.58 | 4.19 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.48 | 14.80 | 99% | 1.15 | 1.74 | 5.07 | 131.2 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.97 | 17.58 | 99% | 2.84 | 2.47 | 5.59 | 178.0 → 163.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.15 | 17.79 | 99% | 0.69 | 1.65 | 4.76 | 116.5 → 117.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.85 | 14.53 | 99% | 0.63 | 1.43 | 4.89 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 1.97 | 15.70 | 99% | 0.74 | 1.51 | 4.80 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.09 | 17.68 | 99% | 1.47 | 1.91 | 5.43 | 118.5 → 115.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.50 | 13.31 | 99% | 1.29 | 1.74 | 5.02 | 121.5 → 117.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.02 | 10.53 | 99% | 0.59 | 1.60 | 4.82 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.26 | 15.66 | 99% | 0.74 | 1.70 | 5.06 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.01 | 17.91 | 99% | 0.82 | 1.56 | 4.29 | 121.3 → 117.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.39 | 76.38 | 99% | 1.38 | 1.48 | 4.30 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 2.01 | 14.98 | 99% | 0.60 | 1.57 | 4.41 | 117.2 → 117.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 5.67 | 24.29 | 96% | 1.03 | 4.63 | 8.49 | 593.4 → 616.2 | 13 | 3.5 / 31 |

