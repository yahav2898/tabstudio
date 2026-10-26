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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.21 | 11.95 | 99% | 0.47 | 1.84 | 4.45 | 123.1 → 130.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.41 | 15.22 | 99% | 0.64 | 1.88 | 4.73 | 159.9 → 157.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 4.82 | 17.78 | 99% | 2.78 | 2.96 | 5.57 | 207.9 → 192.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.32 | 15.23 | 99% | 0.61 | 1.88 | 4.60 | 144.9 → 146.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.11 | 11.60 | 99% | 0.59 | 1.71 | 4.92 | 119.3 → 117.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.38 | 16.46 | 99% | 0.63 | 1.81 | 5.19 | 144.8 → 140.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.80 | 2.94 | 14.93 | 99% | 0.68 | 2.03 | 4.91 | 146.6 → 143.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.95 | 15.10 | 99% | 0.68 | 2.43 | 5.10 | 150.3 → 146.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.09 | 10.46 | 99% | 0.47 | 1.74 | 4.36 | 108.6 → 106.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.26 | 12.80 | 99% | 0.63 | 1.83 | 4.68 | 149.1 → 146.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.19 | 14.83 | 99% | 0.61 | 1.81 | 4.20 | 149.2 → 146.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.38 | 13.98 | 100% | 0.52 | 1.69 | 4.10 | 69.5 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.218 |
| AttachingStyle - | 0.024 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable ContentPresenter | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.33 | 15.53 | 99% | 0.64 | 1.85 | 4.84 | 145.4 → 146.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 5.46 | 25.06 | 95% | 0.79 | 4.03 | 5.12 | 551.9 → 570.2 | 13 | 3.5 / 31 |

