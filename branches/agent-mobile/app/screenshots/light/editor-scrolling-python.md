# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.56 | 9.35 | 99% | 0.27 | 1.39 | 2.35 | 120.9 → 128.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.48 | 1.73 | 11.61 | 99% | 0.43 | 1.48 | 2.81 | 157.8 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.56 | 2.02 | 11.97 | 99% | 0.57 | 1.58 | 3.03 | 205.1 → 190.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.42 | 1.78 | 7.80 | 100% | 0.39 | 1.51 | 2.73 | 143.1 → 144.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 2.43 | 11.95 | 99% | 0.52 | 1.60 | 2.74 | 117.4 → 116.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.46 | 2.33 | 13.07 | 99% | 0.42 | 1.82 | 2.86 | 143.0 → 138.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.48 | 11.39 | 99% | 0.37 | 1.16 | 2.82 | 144.4 → 141.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.40 | 1.37 | 10.97 | 99% | 0.40 | 1.08 | 2.86 | 147.3 → 144.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.32 | 1.22 | 7.57 | 100% | 0.27 | 1.05 | 2.57 | 106.2 → 104.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 1.28 | 10.65 | 99% | 0.37 | 1.06 | 2.78 | 146.9 → 143.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.44 | 2.71 | 11.66 | 99% | 0.62 | 2.25 | 2.48 | 146.9 → 144.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 1.67 | 7.12 | 100% | 0.36 | 1.17 | 2.31 | 68.0 → 66.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.150 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.37 | 1.32 | 13.14 | 100% | 0.35 | 1.07 | 2.66 | 143.0 → 144.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 3.63 | 35.19 | 96% | 0.43 | 2.99 | 2.97 | 544.1 → 561.2 | 13 | 3.5 / 31 |

