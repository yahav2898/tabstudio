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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.60 | 11.01 | 99% | 0.41 | 2.27 | 4.19 | 124.9 → 132.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 4.44 | 13.63 | 99% | 0.78 | 2.68 | 4.92 | 161.9 → 159.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 2.69 | 14.85 | 99% | 0.85 | 2.07 | 4.80 | 208.8 → 193.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.78 | 13.88 | 99% | 0.59 | 2.37 | 4.63 | 146.8 → 148.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.63 | 12.36 | 99% | 0.62 | 2.21 | 4.57 | 121.3 → 119.8 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 3.65 | 15.38 | 99% | 0.71 | 2.58 | 5.16 | 147.1 → 142.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.72 | 13.52 | 99% | 0.60 | 2.34 | 4.68 | 148.6 → 145.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.63 | 14.47 | 99% | 0.60 | 2.31 | 4.58 | 151.2 → 148.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 3.10 | 11.92 | 99% | 0.58 | 2.52 | 4.74 | 110.5 → 109.2 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.55 | 11.75 | 99% | 0.61 | 2.25 | 4.52 | 150.9 → 148.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.72 | 14.58 | 99% | 0.61 | 2.28 | 4.22 | 151.1 → 148.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.20 | 6.68 | 100% | 0.45 | 1.98 | 3.83 | 71.3 → 70.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.340 |
| ArrangingLayoutable CachedLineNumberMargin | 0.042 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable TextView | 0.027 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.81 | 3.15 | 12.46 | 99% | 0.63 | 2.67 | 4.79 | 147.7 → 148.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.18 | 5.34 | 25.24 | 96% | 0.76 | 4.37 | 4.86 | 550.8 → 569.5 | 13 | 3.5 / 31 |

