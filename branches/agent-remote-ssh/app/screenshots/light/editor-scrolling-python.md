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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 2.86 | 12.75 | 99% | 0.63 | 2.24 | 5.40 | 126.1 → 133.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.27 | 3.56 | 15.94 | 99% | 1.03 | 2.40 | 5.67 | 163.4 → 160.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.25 | 15.15 | 99% | 1.16 | 2.31 | 5.73 | 209.8 → 195.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 2.83 | 14.46 | 99% | 0.78 | 2.12 | 5.38 | 147.5 → 148.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.89 | 3.34 | 12.23 | 99% | 0.80 | 2.39 | 5.30 | 122.0 → 119.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.53 | 16.26 | 99% | 0.75 | 1.93 | 5.20 | 146.5 → 142.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 2.62 | 15.03 | 99% | 0.74 | 2.02 | 5.22 | 148.6 → 145.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.96 | 16.92 | 99% | 0.88 | 2.76 | 5.58 | 152.5 → 149.3 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.94 | 2.52 | 10.97 | 99% | 0.64 | 1.98 | 5.05 | 110.5 → 109.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.12 | 2.83 | 15.19 | 99% | 0.81 | 2.14 | 5.45 | 151.8 → 148.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 4.55 | 15.76 | 99% | 0.86 | 2.77 | 4.93 | 151.5 → 148.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 2.31 | 23.37 | 99% | 0.60 | 1.76 | 4.68 | 71.9 → 71.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.07 | 2.92 | 16.25 | 99% | 0.82 | 2.14 | 5.58 | 147.6 → 148.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.07 | 7.05 | 82.18 | 96% | 2.50 | 6.17 | 5.65 | 554.9 → 565.3 | 12 | 3.5 / 31 |

