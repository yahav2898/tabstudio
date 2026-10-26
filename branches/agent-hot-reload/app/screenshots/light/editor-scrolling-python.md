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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 2.44 | 14.08 | 99% | 0.53 | 2.06 | 4.81 | 121.9 → 129.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 2.78 | 17.90 | 99% | 0.74 | 2.10 | 5.06 | 158.2 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 2.88 | 18.14 | 99% | 0.94 | 2.21 | 5.26 | 204.7 → 190.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.79 | 15.27 | 99% | 0.85 | 2.63 | 5.35 | 144.3 → 145.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.25 | 12.61 | 99% | 0.62 | 1.82 | 4.93 | 118.5 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 2.61 | 18.84 | 99% | 0.73 | 1.99 | 5.28 | 143.2 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.55 | 14.83 | 99% | 0.65 | 2.04 | 5.03 | 145.1 → 141.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 3.38 | 18.34 | 99% | 0.77 | 2.56 | 5.41 | 148.0 → 145.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.96 | 3.23 | 12.04 | 99% | 0.71 | 2.29 | 5.16 | 108.2 → 106.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 2.91 | 16.59 | 99% | 0.78 | 2.26 | 5.33 | 148.6 → 145.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 2.59 | 16.57 | 99% | 0.74 | 2.08 | 4.35 | 147.8 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.01 | 12.00 | 99% | 0.51 | 1.64 | 4.19 | 69.0 → 68.7 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.359 |
| AttachingStyle - | 0.056 |
| ArrangingLayoutable LineDrawing | 0.054 |
| ArrangingLayoutable TextView | 0.034 |
| FindingResource - | 0.024 |
| ArrangingLayoutable CachedLineNumberMargin | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable Track | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 2.70 | 17.30 | 99% | 0.76 | 2.11 | 4.98 | 144.0 → 145.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.47 | 5.72 | 37.86 | 96% | 0.86 | 4.56 | 5.54 | 543.7 → 561.6 | 13 | 3.5 / 31 |

