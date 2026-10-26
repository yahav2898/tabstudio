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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 2.53 | 14.51 | 99% | 0.54 | 2.08 | 4.99 | 128.8 → 136.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 3.68 | 15.58 | 99% | 0.85 | 2.56 | 5.78 | 165.6 → 164.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.14 | 3.63 | 15.07 | 98% | 1.18 | 2.29 | 5.78 | 211.8 → 196.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 2.60 | 14.96 | 99% | 0.74 | 2.05 | 5.02 | 150.4 → 151.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 2.50 | 14.01 | 99% | 0.73 | 1.92 | 5.22 | 125.5 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 3.14 | 18.04 | 99% | 0.86 | 2.31 | 5.23 | 149.9 → 146.5 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.03 | 3.47 | 81.60 | 99% | 0.89 | 2.60 | 5.89 | 153.0 → 149.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 3.22 | 24.99 | 99% | 0.81 | 2.36 | 5.96 | 155.7 → 152.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 2.67 | 12.46 | 100% | 0.66 | 2.10 | 5.36 | 114.9 → 112.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.21 | 4.79 | 79.00 | 99% | 0.97 | 3.21 | 5.94 | 155.7 → 152.9 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 2.73 | 17.33 | 99% | 0.81 | 2.10 | 5.14 | 154.6 → 151.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.28 | 12.42 | 99% | 0.55 | 1.80 | 4.27 | 75.8 → 75.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.261 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 3.00 | 17.60 | 99% | 0.77 | 2.24 | 5.38 | 150.9 → 152.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.52 | 6.22 | 78.39 | 95% | 0.83 | 4.87 | 5.18 | 551.0 → 567.9 | 13 | 3.5 / 31 |

