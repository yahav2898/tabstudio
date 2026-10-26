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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 1.80 | 9.77 | 99% | 0.39 | 1.47 | 3.63 | 122.1 → 129.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 2.05 | 12.48 | 99% | 0.58 | 1.57 | 3.82 | 158.6 → 156.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 3.32 | 16.87 | 99% | 0.90 | 1.96 | 4.39 | 206.1 → 191.3 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.85 | 12.41 | 99% | 0.62 | 2.40 | 4.27 | 144.7 → 145.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.05 | 12.86 | 99% | 0.56 | 1.59 | 4.21 | 118.8 → 117.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 1.96 | 14.21 | 99% | 0.55 | 1.55 | 3.87 | 143.5 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.13 | 12.98 | 99% | 0.61 | 1.67 | 4.15 | 146.0 → 142.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.39 | 17.79 | 99% | 0.73 | 2.45 | 4.60 | 148.9 → 146.2 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.28 | 10.30 | 99% | 0.51 | 1.81 | 4.24 | 108.3 → 106.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.28 | 12.51 | 99% | 0.63 | 1.78 | 4.22 | 148.7 → 145.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.84 | 2.31 | 15.97 | 99% | 0.63 | 1.80 | 3.79 | 148.9 → 145.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 1.87 | 8.12 | 100% | 0.49 | 1.50 | 3.61 | 68.9 → 68.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.276 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| ArrangingLayoutable TextView | 0.020 |
| RaisingRoutedEvent ScrollBar | 0.015 |
| FindingResource - | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.39 | 15.65 | 99% | 2.22 | 2.39 | 4.59 | 145.3 → 145.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.04 | 4.82 | 26.05 | 96% | 0.70 | 3.89 | 4.78 | 548.3 → 571.6 | 13 | 3.5 / 31 |

