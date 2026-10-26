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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.08 | 10.73 | 99% | 0.43 | 1.77 | 4.44 | 122.6 → 129.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.90 | 14.44 | 99% | 0.67 | 2.18 | 4.88 | 158.5 → 156.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.61 | 14.12 | 99% | 1.03 | 1.90 | 5.03 | 205.5 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.31 | 13.93 | 99% | 0.59 | 1.88 | 4.70 | 143.8 → 145.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.73 | 13.36 | 99% | 0.56 | 2.31 | 4.70 | 118.7 → 117.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 3.07 | 17.36 | 99% | 0.68 | 2.51 | 5.19 | 143.8 → 139.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.93 | 15.92 | 99% | 0.67 | 2.51 | 5.29 | 146.0 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.75 | 13.93 | 99% | 0.61 | 2.29 | 4.82 | 148.4 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.65 | 10.42 | 100% | 0.45 | 2.34 | 4.52 | 108.1 → 106.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 4.67 | 17.57 | 99% | 0.75 | 4.06 | 4.85 | 148.4 → 146.0 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.78 | 14.53 | 99% | 0.61 | 2.40 | 4.22 | 148.1 → 144.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.26 | 9.73 | 100% | 0.41 | 2.05 | 3.87 | 69.5 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.13 | 18.40 | 99% | 0.64 | 2.57 | 4.89 | 144.1 → 145.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.19 | 6.87 | 22.74 | 95% | 0.68 | 5.25 | 5.05 | 548.5 → 571.2 | 13 | 3.5 / 31 |

