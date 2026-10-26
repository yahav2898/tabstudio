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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 1.63 | 57.33 | 100% | 0.37 | 1.34 | 3.86 | 128.2 → 135.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 1.77 | 48.02 | 100% | 0.53 | 1.34 | 4.51 | 164.4 → 162.3 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 1.93 | 2.48 | 100% | 0.71 | 1.35 | 4.59 | 211.2 → 196.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.38 | 57.18 | 100% | 2.40 | 2.53 | 4.60 | 150.4 → 151.6 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.60 | 41.70 | 100% | 0.50 | 1.22 | 4.32 | 124.4 → 122.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 1.65 | 2.02 | 100% | 0.53 | 1.27 | 3.89 | 148.9 → 145.2 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 1.72 | 49.00 | 100% | 0.55 | 1.36 | 4.44 | 151.3 → 148.1 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.60 | 1.69 | 47.60 | 100% | 0.52 | 1.31 | 4.36 | 153.9 → 151.2 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 1.69 | 3.69 | 100% | 0.41 | 1.31 | 4.30 | 113.8 → 112.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 3.23 | 12.08 | 100% | 0.79 | 2.48 | 4.77 | 155.0 → 151.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 1.71 | 45.16 | 100% | 0.54 | 1.35 | 3.81 | 153.6 → 150.5 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.31 | 1.63 | 100% | 0.38 | 1.07 | 3.57 | 74.9 → 74.2 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.173 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.65 | 1.77 | 43.66 | 100% | 0.53 | 1.38 | 4.57 | 150.4 → 151.1 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.98 | 5.21 | 77.31 | 98% | 0.69 | 4.78 | 5.34 | 558.1 → 575.2 | 4 | 3.5 / 31 |

