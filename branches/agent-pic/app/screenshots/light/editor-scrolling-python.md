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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.46 | 9.42 | 99% | 0.39 | 2.20 | 4.25 | 122.1 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.22 | 18.14 | 99% | 0.73 | 2.60 | 5.08 | 158.5 → 156.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.00 | 15.68 | 99% | 0.88 | 2.47 | 5.16 | 204.7 → 189.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.69 | 12.03 | 99% | 0.59 | 2.36 | 4.60 | 143.4 → 144.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.00 | 14.08 | 99% | 0.69 | 2.39 | 5.09 | 118.6 → 117.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.91 | 14.39 | 99% | 0.59 | 2.56 | 5.15 | 143.4 → 139.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.78 | 12.71 | 99% | 0.58 | 2.42 | 4.98 | 145.1 → 141.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.74 | 13.77 | 99% | 0.61 | 2.40 | 4.75 | 148.0 → 144.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.67 | 9.26 | 100% | 0.57 | 2.60 | 4.81 | 107.9 → 106.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.69 | 11.95 | 99% | 0.61 | 2.33 | 4.63 | 147.6 → 144.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.43 | 15.12 | 99% | 0.59 | 2.14 | 4.26 | 147.8 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.18 | 11.24 | 99% | 0.42 | 1.99 | 3.96 | 69.3 → 68.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.299 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.028 |
| FindingResource - | 0.022 |
| RaisingRoutedEvent ScrollViewer | 0.021 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.74 | 13.41 | 99% | 0.57 | 2.38 | 4.89 | 143.9 → 144.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.19 | 5.37 | 34.35 | 96% | 0.72 | 4.44 | 5.12 | 543.6 → 566.0 | 13 | 3.5 / 31 |

