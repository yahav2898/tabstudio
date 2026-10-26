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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.14 | 45.03 | 100% | 0.44 | 1.80 | 3.94 | 122.8 → 130.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.14 | 48.30 | 100% | 0.65 | 1.71 | 4.66 | 159.2 → 157.1 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.59 | 5.06 | 100% | 0.98 | 2.48 | 4.92 | 206.2 → 191.6 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.79 | 12.10 | 100% | 0.67 | 2.43 | 4.70 | 144.8 → 145.5 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 1.82 | 2.21 | 100% | 0.58 | 1.40 | 4.52 | 118.6 → 117.1 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.25 | 38.98 | 100% | 0.61 | 1.86 | 4.68 | 144.0 → 140.0 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 2.07 | 44.24 | 100% | 0.64 | 1.58 | 4.60 | 146.1 → 142.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 4.02 | 5.44 | 100% | 1.73 | 2.53 | 4.97 | 149.4 → 146.0 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 1.99 | 25.52 | 100% | 0.50 | 1.63 | 4.41 | 108.0 → 106.4 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.14 | 40.47 | 100% | 0.63 | 1.69 | 4.78 | 148.7 → 145.9 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.14 | 2.58 | 100% | 0.61 | 1.75 | 4.26 | 148.6 → 145.2 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.94 | 40.96 | 100% | 0.47 | 1.58 | 3.95 | 68.7 → 68.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.314 |
| AttachingStyle - | 0.038 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable RepeatButton | 0.013 |
| ArrangingLayoutable PageHost | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.09 | 35.88 | 100% | 0.61 | 1.59 | 4.71 | 144.8 → 145.7 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.18 | 4.38 | 68.52 | 98% | 0.70 | 3.61 | 5.13 | 543.8 → 571.0 | 3 | 3.5 / 31 |

