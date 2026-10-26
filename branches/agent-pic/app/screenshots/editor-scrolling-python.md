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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.01 | 3.23 | 11.23 | 99% | 0.60 | 2.66 | 5.11 | 122.6 → 130.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 3.91 | 16.31 | 99% | 0.89 | 2.91 | 5.40 | 160.1 → 157.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.45 | 3.33 | 16.47 | 99% | 1.14 | 2.30 | 5.68 | 206.3 → 191.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 3.07 | 16.84 | 99% | 0.83 | 2.27 | 5.21 | 144.1 → 146.0 | 5 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 2.97 | 10.74 | 99% | 0.83 | 2.07 | 5.22 | 120.1 → 117.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 2.83 | 13.65 | 99% | 0.82 | 2.13 | 5.16 | 143.7 → 139.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.18 | 3.02 | 21.06 | 99% | 0.86 | 2.27 | 5.44 | 146.2 → 143.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.32 | 4.50 | 16.79 | 99% | 0.92 | 3.61 | 5.61 | 149.8 → 146.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 2.70 | 11.41 | 99% | 0.65 | 2.10 | 5.04 | 108.3 → 106.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 2.97 | 14.30 | 99% | 0.82 | 2.23 | 5.31 | 148.6 → 145.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.18 | 3.34 | 13.94 | 99% | 0.91 | 2.43 | 4.91 | 148.6 → 145.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.40 | 8.01 | 100% | 0.60 | 1.84 | 4.36 | 70.3 → 69.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.318 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.12 | 2.96 | 17.70 | 99% | 0.82 | 2.20 | 5.14 | 144.8 → 145.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.48 | 8.44 | 83.70 | 95% | 2.58 | 6.63 | 5.78 | 550.4 → 573.9 | 13 | 3.5 / 31 |

