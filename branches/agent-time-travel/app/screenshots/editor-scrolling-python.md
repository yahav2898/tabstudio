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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 1.85 | 54.29 | 100% | 0.44 | 1.54 | 4.25 | 122.9 → 130.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.32 | 3.74 | 100% | 0.65 | 1.85 | 5.05 | 159.7 → 157.5 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 4.43 | 47.35 | 100% | 2.71 | 3.56 | 5.32 | 207.4 → 193.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.41 | 35.98 | 100% | 0.64 | 1.94 | 4.83 | 144.7 → 146.2 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 1.98 | 2.69 | 100% | 0.61 | 1.61 | 4.83 | 119.4 → 117.5 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.19 | 41.79 | 100% | 0.62 | 1.82 | 4.75 | 144.3 → 140.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.27 | 49.22 | 100% | 0.64 | 1.57 | 4.85 | 146.5 → 143.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.26 | 6.76 | 100% | 0.78 | 2.54 | 5.07 | 150.1 → 146.1 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.21 | 35.83 | 100% | 0.47 | 1.83 | 4.74 | 108.6 → 106.7 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.26 | 43.75 | 100% | 0.63 | 1.83 | 5.22 | 149.1 → 146.2 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.05 | 2.62 | 100% | 0.62 | 1.59 | 3.94 | 149.1 → 145.7 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.92 | 4.26 | 100% | 0.49 | 1.60 | 4.06 | 69.3 → 68.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| AttachingStyle - | 0.375 |
| MeasuringLayoutable TextView | 0.254 |
| ArrangingLayoutable CachedLineNumberMargin | 0.041 |
| MeasuringLayoutable NumberDrawing | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.17 | 43.69 | 100% | 0.60 | 1.64 | 4.78 | 145.2 → 146.2 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.09 | 4.25 | 69.59 | 98% | 0.69 | 3.66 | 5.15 | 551.4 → 571.8 | 3 | 3.5 / 31 |

