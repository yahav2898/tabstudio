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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.14 | 12.71 | 99% | 0.40 | 1.83 | 4.22 | 122.3 → 129.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.68 | 15.01 | 99% | 0.79 | 2.82 | 5.29 | 159.1 → 157.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.04 | 16.61 | 99% | 0.87 | 2.46 | 4.93 | 205.3 → 190.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.53 | 15.12 | 99% | 0.59 | 2.12 | 4.87 | 143.8 → 145.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.75 | 14.43 | 99% | 0.57 | 2.32 | 4.94 | 118.9 → 117.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 4.92 | 12.94 | 99% | 2.23 | 4.01 | 4.90 | 143.9 → 140.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.74 | 13.81 | 99% | 0.59 | 2.41 | 4.67 | 145.4 → 142.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.78 | 17.04 | 99% | 0.58 | 2.40 | 4.84 | 148.5 → 145.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.71 | 11.77 | 100% | 0.42 | 2.38 | 4.59 | 108.1 → 106.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 4.34 | 15.10 | 99% | 0.88 | 2.82 | 5.08 | 149.0 → 145.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.67 | 16.25 | 99% | 0.61 | 2.35 | 4.28 | 148.1 → 145.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.33 | 11.43 | 100% | 0.39 | 2.09 | 3.89 | 69.4 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.230 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.61 | 13.00 | 99% | 0.79 | 2.75 | 5.16 | 144.5 → 146.2 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.20 | 5.26 | 33.57 | 96% | 0.73 | 4.60 | 5.02 | 550.8 → 571.9 | 13 | 3.5 / 31 |

