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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.76 | 2.21 | 11.46 | 99% | 0.49 | 1.82 | 4.73 | 125.1 → 132.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 4.41 | 71.87 | 99% | 1.00 | 2.52 | 5.62 | 162.7 → 160.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 2.75 | 14.47 | 99% | 0.95 | 1.99 | 5.37 | 209.9 → 194.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 2.62 | 13.06 | 99% | 0.76 | 2.01 | 5.04 | 147.4 → 148.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 2.60 | 12.77 | 99% | 0.71 | 1.78 | 5.25 | 121.7 → 120.3 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 3.19 | 14.26 | 99% | 0.71 | 2.52 | 5.55 | 147.5 → 143.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.38 | 14.24 | 99% | 0.68 | 1.88 | 5.13 | 149.0 → 145.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.37 | 13.82 | 99% | 0.68 | 1.87 | 5.08 | 151.6 → 148.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.80 | 3.07 | 71.17 | 99% | 0.65 | 2.50 | 5.12 | 110.9 → 109.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.53 | 11.88 | 99% | 0.73 | 1.92 | 4.95 | 151.5 → 148.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 2.37 | 13.53 | 99% | 0.65 | 1.82 | 4.55 | 151.5 → 148.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.75 | 9.34 | 100% | 0.45 | 1.47 | 3.98 | 71.7 → 71.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.363 |
| AttachingStyle - | 0.101 |
| ArrangingLayoutable LineDrawing | 0.031 |
| FindingResource - | 0.024 |
| ArrangingLayoutable TextView | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| MeasuringLayoutable ScrollBar | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 2.36 | 11.39 | 99% | 0.69 | 1.83 | 5.02 | 147.4 → 148.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.20 | 4.54 | 28.13 | 96% | 0.76 | 3.76 | 5.14 | 556.9 → 575.6 | 13 | 3.5 / 31 |

