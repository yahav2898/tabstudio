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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.06 | 11.83 | 99% | 0.39 | 1.78 | 4.24 | 122.0 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.22 | 14.74 | 99% | 0.61 | 1.77 | 4.53 | 158.1 → 155.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.73 | 16.52 | 99% | 0.94 | 2.47 | 5.16 | 204.4 → 190.5 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.72 | 72.50 | 99% | 0.62 | 2.14 | 4.98 | 143.8 → 144.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.00 | 16.00 | 99% | 0.56 | 1.63 | 4.80 | 118.2 → 116.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.21 | 16.60 | 99% | 0.68 | 1.77 | 5.02 | 143.0 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.42 | 17.32 | 99% | 0.60 | 1.93 | 4.74 | 145.1 → 141.9 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 4.21 | 14.45 | 99% | 0.76 | 2.45 | 5.15 | 148.4 → 144.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 2.01 | 9.19 | 100% | 0.43 | 1.69 | 4.69 | 107.7 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 2.22 | 16.10 | 99% | 0.58 | 1.76 | 4.48 | 147.6 → 144.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.19 | 13.93 | 99% | 0.58 | 1.73 | 4.03 | 147.6 → 144.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.49 | 9.44 | 99% | 0.55 | 1.86 | 4.18 | 69.2 → 68.9 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.246 |
| AttachingStyle - | 0.083 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.21 | 14.68 | 99% | 0.59 | 1.75 | 4.50 | 143.7 → 144.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.03 | 4.57 | 36.46 | 96% | 0.69 | 3.89 | 5.14 | 543.8 → 564.5 | 13 | 3.5 / 31 |

