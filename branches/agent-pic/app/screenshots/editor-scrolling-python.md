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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.02 | 11.24 | 99% | 0.45 | 1.65 | 4.41 | 123.1 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 4.49 | 17.89 | 99% | 0.98 | 2.71 | 5.02 | 160.2 → 158.1 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 2.41 | 13.79 | 99% | 0.82 | 1.74 | 4.77 | 206.8 → 192.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.29 | 13.83 | 99% | 0.65 | 1.78 | 4.66 | 144.9 → 146.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.09 | 10.62 | 99% | 0.60 | 1.61 | 4.47 | 119.2 → 117.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 3.31 | 12.44 | 99% | 0.65 | 2.46 | 5.05 | 144.9 → 140.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.17 | 13.40 | 99% | 0.62 | 1.69 | 4.66 | 146.5 → 143.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.16 | 16.25 | 99% | 0.67 | 1.73 | 4.61 | 149.3 → 146.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.95 | 14.04 | 99% | 0.62 | 2.06 | 4.69 | 108.6 → 107.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.22 | 13.16 | 99% | 0.63 | 1.75 | 4.74 | 149.1 → 146.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.25 | 16.85 | 99% | 0.67 | 1.80 | 4.42 | 149.2 → 145.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.75 | 6.82 | 100% | 0.45 | 1.42 | 3.75 | 69.1 → 68.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.278 |
| ArrangingLayoutable NumberDrawing | 0.040 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable TextView | 0.015 |
| MeasuringLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable Thumb | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 3.14 | 12.67 | 99% | 0.71 | 2.21 | 5.03 | 145.6 → 146.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.18 | 5.86 | 22.35 | 96% | 0.67 | 4.39 | 5.02 | 557.6 → 573.0 | 13 | 3.5 / 31 |

