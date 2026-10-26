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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.57 | 12.52 | 99% | 0.57 | 2.03 | 4.82 | 125.3 → 132.8 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.20 | 4.70 | 17.11 | 99% | 0.95 | 2.65 | 5.30 | 163.3 → 160.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 2.80 | 16.31 | 99% | 0.92 | 2.00 | 4.93 | 209.7 → 194.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.15 | 13.26 | 99% | 0.61 | 1.73 | 4.54 | 147.1 → 148.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.56 | 11.47 | 99% | 0.63 | 1.69 | 4.57 | 121.5 → 120.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.95 | 2.91 | 13.66 | 99% | 0.74 | 2.03 | 4.88 | 147.2 → 142.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.24 | 13.52 | 99% | 0.62 | 1.80 | 4.70 | 148.8 → 145.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.20 | 13.54 | 99% | 0.63 | 1.76 | 4.72 | 151.8 → 148.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.85 | 3.34 | 70.07 | 99% | 0.61 | 2.52 | 5.10 | 111.2 → 109.7 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 2.27 | 10.63 | 99% | 0.62 | 1.82 | 4.62 | 151.3 → 148.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 2.34 | 12.96 | 99% | 0.68 | 1.85 | 4.27 | 151.4 → 148.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.65 | 8.24 | 100% | 0.45 | 1.38 | 3.78 | 71.3 → 70.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.373 |
| FindingResource - | 0.046 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.018 |
| ArrangingLayoutable Track | 0.018 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| RaisingRoutedEvent Border | 0.010 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 2.90 | 13.00 | 99% | 0.70 | 2.10 | 4.81 | 148.2 → 148.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 5.33 | 22.60 | 96% | 0.69 | 4.71 | 4.95 | 553.4 → 577.5 | 13 | 3.5 / 31 |

