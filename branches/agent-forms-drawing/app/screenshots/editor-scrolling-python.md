# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.10 | 11.00 | 99% | 0.43 | 1.79 | 4.50 | 128.8 → 136.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.36 | 13.64 | 99% | 0.65 | 1.84 | 4.86 | 165.3 → 163.1 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.23 | 4.68 | 17.64 | 99% | 2.63 | 2.78 | 5.52 | 213.1 → 197.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.18 | 12.35 | 99% | 0.58 | 1.73 | 4.81 | 150.4 → 151.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.05 | 12.42 | 99% | 0.58 | 1.59 | 4.80 | 125.3 → 123.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 2.22 | 16.07 | 99% | 0.63 | 1.78 | 4.75 | 149.9 → 146.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.90 | 18.13 | 99% | 0.69 | 2.10 | 4.98 | 152.1 → 149.3 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.11 | 80.11 | 99% | 0.61 | 2.43 | 4.98 | 155.5 → 151.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.09 | 9.98 | 99% | 0.46 | 1.67 | 4.57 | 114.6 → 113.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.21 | 13.04 | 99% | 0.61 | 1.79 | 4.91 | 154.8 → 151.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.35 | 14.32 | 99% | 0.64 | 1.84 | 4.32 | 154.5 → 151.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 2.73 | 13.41 | 100% | 0.65 | 2.30 | 4.44 | 76.8 → 76.1 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.253 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.27 | 13.10 | 99% | 0.60 | 1.79 | 4.79 | 151.0 → 151.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.16 | 5.09 | 23.32 | 96% | 0.74 | 4.50 | 5.18 | 558.1 → 574.7 | 13 | 3.5 / 31 |

