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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 4.20 | 13.18 | 99% | 0.76 | 2.75 | 5.41 | 122.9 → 130.7 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 2.70 | 14.32 | 99% | 0.77 | 2.01 | 4.87 | 158.7 → 156.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 2.82 | 14.92 | 99% | 0.96 | 2.00 | 4.99 | 205.3 → 190.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 2.64 | 11.78 | 99% | 0.72 | 2.03 | 4.93 | 143.9 → 145.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.22 | 12.38 | 99% | 0.76 | 2.49 | 5.20 | 119.4 → 117.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.93 | 2.71 | 16.16 | 99% | 0.76 | 2.02 | 5.59 | 143.4 → 139.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 2.62 | 17.44 | 99% | 0.75 | 2.01 | 5.10 | 145.7 → 142.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.01 | 14.31 | 99% | 0.75 | 2.18 | 4.99 | 148.4 → 145.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.66 | 76.01 | 99% | 0.52 | 2.14 | 4.65 | 108.8 → 106.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 2.34 | 11.84 | 99% | 0.62 | 1.85 | 4.80 | 147.9 → 145.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 2.33 | 14.30 | 99% | 0.65 | 1.85 | 4.26 | 148.0 → 144.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 2.07 | 7.25 | 100% | 0.56 | 1.62 | 3.94 | 69.3 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.381 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 2.46 | 15.13 | 99% | 0.70 | 1.92 | 4.82 | 144.2 → 145.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 5.06 | 23.49 | 96% | 0.79 | 4.11 | 4.96 | 545.8 → 568.5 | 12 | 3.5 / 31 |

