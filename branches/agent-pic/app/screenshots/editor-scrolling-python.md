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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 1.92 | 27.41 | 100% | 0.41 | 1.54 | 3.63 | 122.6 → 129.8 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 2.05 | 3.37 | 100% | 0.58 | 1.62 | 3.85 | 159.3 → 157.1 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.15 | 2.83 | 100% | 0.79 | 1.67 | 4.49 | 206.6 → 191.5 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 1.95 | 44.17 | 100% | 0.56 | 1.56 | 4.08 | 144.4 → 145.6 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 3.27 | 12.98 | 100% | 0.75 | 2.31 | 4.30 | 119.1 → 117.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 1.79 | 2.55 | 100% | 0.56 | 1.41 | 4.00 | 144.0 → 139.8 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 1.71 | 40.71 | 100% | 0.56 | 1.26 | 4.26 | 145.8 → 142.5 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 1.75 | 43.76 | 100% | 0.58 | 1.34 | 3.85 | 148.5 → 145.4 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.52 | 1.76 | 100% | 0.42 | 1.22 | 3.51 | 107.6 → 105.8 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 3.69 | 40.28 | 99% | 2.35 | 2.53 | 4.43 | 148.5 → 146.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.66 | 2.65 | 100% | 0.56 | 1.27 | 3.78 | 148.1 → 145.0 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.31 | 37.09 | 100% | 0.39 | 1.03 | 3.54 | 68.6 → 67.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.187 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 1.71 | 3.32 | 100% | 0.57 | 1.30 | 4.03 | 144.5 → 145.3 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.34 | 6.25 | 62.02 | 97% | 0.75 | 5.70 | 4.81 | 552.8 → 576.6 | 4 | 3.5 / 31 |

