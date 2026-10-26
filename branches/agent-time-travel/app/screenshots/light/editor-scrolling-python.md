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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 1.69 | 2.07 | 100% | 0.42 | 1.41 | 4.36 | 122.7 → 130.0 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 1.95 | 47.44 | 100% | 0.64 | 1.45 | 4.40 | 159.0 → 156.9 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 2.21 | 51.98 | 100% | 0.87 | 1.47 | 5.06 | 206.2 → 191.2 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.73 | 4.26 | 100% | 0.91 | 3.12 | 5.06 | 144.7 → 145.9 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 1.74 | 5.03 | 100% | 0.59 | 1.31 | 4.81 | 119.0 → 116.9 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 1.80 | 42.02 | 100% | 0.62 | 1.41 | 4.72 | 143.8 → 140.0 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 1.89 | 49.63 | 100% | 0.63 | 1.41 | 4.83 | 146.0 → 142.9 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.46 | 4.52 | 100% | 0.63 | 1.65 | 4.91 | 148.9 → 145.9 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.71 | 13.05 | 100% | 0.53 | 2.33 | 4.29 | 108.0 → 106.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 1.86 | 2.16 | 100% | 0.61 | 1.42 | 4.27 | 148.6 → 145.8 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 1.97 | 43.29 | 100% | 0.65 | 1.49 | 4.66 | 148.6 → 145.0 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.44 | 1.87 | 100% | 0.44 | 1.14 | 3.98 | 68.7 → 68.1 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.217 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.50 | 24.81 | 100% | 0.70 | 2.59 | 4.96 | 145.0 → 145.8 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.08 | 3.93 | 65.66 | 99% | 0.69 | 3.53 | 5.12 | 548.1 → 565.5 | 2 | 3.5 / 31 |

