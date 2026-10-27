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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.34 | 8.92 | 100% | 0.27 | 1.14 | 2.85 | 126.9 → 133.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.42 | 1.46 | 11.04 | 99% | 0.38 | 1.17 | 2.80 | 162.7 → 160.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 1.60 | 12.53 | 99% | 0.54 | 1.20 | 2.90 | 209.0 → 194.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.41 | 1.50 | 9.63 | 99% | 0.39 | 1.23 | 2.85 | 148.2 → 149.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.34 | 1.33 | 10.17 | 100% | 0.39 | 1.10 | 2.85 | 122.8 → 121.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 2.87 | 13.53 | 99% | 0.50 | 2.34 | 3.07 | 147.9 → 144.5 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.72 | 10.66 | 100% | 0.55 | 2.00 | 3.34 | 151.0 → 147.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 1.75 | 10.83 | 99% | 0.50 | 1.39 | 3.04 | 152.9 → 149.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.42 | 1.60 | 8.66 | 100% | 0.35 | 1.32 | 2.80 | 112.6 → 110.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.54 | 1.81 | 11.80 | 99% | 0.50 | 1.44 | 3.07 | 152.6 → 149.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 1.94 | 13.59 | 99% | 0.53 | 1.52 | 2.97 | 152.7 → 149.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.49 | 9.32 | 99% | 0.41 | 1.23 | 2.42 | 74.2 → 73.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.330 |
| ArrangingLayoutable NumberDrawing | 0.059 |
| ArrangingLayoutable LineDrawing | 0.031 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable MainView | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.23 | 14.32 | 99% | 0.65 | 2.46 | 3.54 | 150.1 → 150.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 6.38 | 23.49 | 96% | 0.59 | 3.88 | 4.64 | 548.7 → 566.9 | 12 | 3.5 / 31 |

