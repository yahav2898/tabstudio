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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 1.72 | 2.00 | 100% | 0.40 | 1.44 | 3.83 | 122.9 → 130.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 1.90 | 43.07 | 100% | 0.61 | 1.45 | 4.12 | 159.6 → 157.4 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 3.04 | 42.37 | 100% | 0.97 | 1.91 | 4.80 | 206.8 → 191.9 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.58 | 4.45 | 100% | 0.78 | 2.47 | 4.74 | 145.0 → 145.6 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 1.65 | 31.43 | 100% | 0.57 | 1.27 | 4.57 | 118.8 → 117.0 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 1.77 | 38.93 | 100% | 0.65 | 1.37 | 4.61 | 143.9 → 140.3 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 1.83 | 2.88 | 100% | 0.64 | 1.46 | 4.63 | 146.4 → 142.9 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 3.33 | 41.81 | 100% | 2.39 | 2.47 | 4.73 | 149.1 → 146.1 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 1.80 | 3.68 | 100% | 0.44 | 1.36 | 3.69 | 107.8 → 106.1 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 1.85 | 30.34 | 100% | 0.59 | 1.42 | 4.54 | 148.9 → 145.7 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 1.79 | 39.03 | 100% | 0.58 | 1.41 | 4.14 | 148.4 → 145.2 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.36 | 1.69 | 100% | 0.41 | 1.09 | 3.83 | 69.1 → 68.5 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.242 |
| ArrangingLayoutable LineDrawing | 0.180 |
| AttachingStyle - | 0.023 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| FindingResource - | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.63 | 6.28 | 100% | 2.38 | 2.57 | 4.72 | 145.6 → 145.9 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.05 | 3.85 | 59.89 | 99% | 0.71 | 3.38 | 5.23 | 550.7 → 574.8 | 2 | 3.5 / 31 |

