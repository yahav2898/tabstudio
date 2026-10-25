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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.04 | 8.99 | 100% | 0.39 | 1.74 | 3.65 | 120.3 → 127.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 2.35 | 13.17 | 99% | 0.58 | 1.95 | 3.92 | 157.1 → 155.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 4.83 | 14.98 | 99% | 2.75 | 2.61 | 4.51 | 205.0 → 189.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.27 | 12.51 | 99% | 0.53 | 1.90 | 3.84 | 142.3 → 143.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.06 | 12.03 | 99% | 0.52 | 1.75 | 3.91 | 116.4 → 114.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 2.07 | 13.95 | 99% | 0.54 | 1.74 | 3.84 | 141.7 → 137.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.98 | 32.03 | 99% | 0.66 | 2.41 | 4.13 | 143.8 → 141.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.34 | 11.64 | 99% | 0.55 | 1.90 | 4.02 | 147.4 → 143.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.93 | 9.19 | 99% | 0.41 | 1.73 | 3.72 | 105.7 → 103.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 2.12 | 14.25 | 99% | 0.56 | 1.76 | 3.84 | 146.6 → 143.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.33 | 12.21 | 99% | 0.54 | 1.86 | 3.57 | 146.2 → 143.2 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.36 | 7.92 | 100% | 0.42 | 1.79 | 3.32 | 67.2 → 66.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.22 | 13.57 | 100% | 0.52 | 1.86 | 4.00 | 142.6 → 143.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.66 | 4.69 | 21.18 | 96% | 0.58 | 4.16 | 4.20 | 548.4 → 575.4 | 12 | 3.5 / 31 |

