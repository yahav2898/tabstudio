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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.03 | 12.23 | 99% | 0.38 | 1.74 | 4.30 | 126.6 → 133.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.20 | 14.99 | 99% | 0.59 | 1.76 | 4.76 | 162.5 → 160.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 4.88 | 16.80 | 99% | 1.77 | 2.81 | 5.31 | 209.3 → 195.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.51 | 15.89 | 99% | 0.57 | 2.20 | 4.63 | 148.0 → 149.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.08 | 12.93 | 99% | 0.55 | 1.67 | 4.68 | 122.8 → 121.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.78 | 17.86 | 99% | 0.57 | 2.37 | 4.67 | 147.3 → 143.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.20 | 17.04 | 99% | 0.68 | 2.67 | 4.90 | 149.7 → 147.1 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.97 | 12.50 | 99% | 0.60 | 2.52 | 4.84 | 153.2 → 149.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 2.65 | 12.79 | 99% | 0.45 | 2.34 | 4.33 | 112.2 → 110.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.79 | 14.90 | 99% | 0.62 | 2.39 | 4.80 | 152.1 → 149.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 3.10 | 18.75 | 99% | 0.66 | 2.61 | 4.41 | 152.2 → 149.5 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.75 | 9.63 | 100% | 0.43 | 2.31 | 4.25 | 74.6 → 72.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| FindingResource - | 0.054 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.68 | 17.28 | 99% | 0.57 | 2.37 | 4.75 | 148.2 → 149.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 7.99 | 28.44 | 95% | 0.73 | 6.91 | 5.21 | 550.1 → 571.3 | 14 | 3.5 / 31 |

