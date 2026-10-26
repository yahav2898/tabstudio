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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.11 | 10.25 | 99% | 0.46 | 1.75 | 4.54 | 124.9 → 132.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 3.76 | 15.63 | 99% | 0.77 | 2.55 | 5.12 | 162.1 → 159.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 2.64 | 14.36 | 99% | 0.88 | 1.89 | 5.07 | 208.6 → 193.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 2.35 | 14.10 | 99% | 0.67 | 1.82 | 4.87 | 146.8 → 148.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.82 | 12.71 | 99% | 0.69 | 1.81 | 5.03 | 121.3 → 120.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.29 | 14.81 | 99% | 0.68 | 1.78 | 5.17 | 146.8 → 142.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.36 | 12.79 | 99% | 0.67 | 1.80 | 4.85 | 148.4 → 145.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.37 | 13.31 | 99% | 0.65 | 1.78 | 4.88 | 151.2 → 148.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.01 | 9.17 | 99% | 0.56 | 2.45 | 4.62 | 111.2 → 108.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.31 | 12.61 | 99% | 0.65 | 1.72 | 4.71 | 151.0 → 148.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.29 | 11.57 | 99% | 0.61 | 1.75 | 4.43 | 151.2 → 147.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.95 | 12.12 | 99% | 0.48 | 1.53 | 3.94 | 70.9 → 71.3 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.618 |
| ArrangingLayoutable Border | 0.038 |
| MeasuringLayoutable NumberDrawing | 0.034 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable TextView | 0.028 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.29 | 11.90 | 99% | 0.63 | 1.75 | 4.76 | 147.1 → 148.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.24 | 6.04 | 21.89 | 96% | 0.75 | 5.02 | 5.20 | 548.7 → 570.8 | 13 | 3.5 / 31 |

