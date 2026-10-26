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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.03 | 2.99 | 12.21 | 99% | 0.63 | 2.35 | 5.39 | 127.3 → 135.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.32 | 3.71 | 18.39 | 99% | 0.99 | 2.77 | 5.77 | 164.4 → 161.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.41 | 3.36 | 19.17 | 99% | 1.09 | 2.37 | 5.93 | 210.6 → 195.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.13 | 16.11 | 99% | 0.83 | 2.36 | 5.43 | 149.0 → 150.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 3.08 | 13.29 | 99% | 0.83 | 2.24 | 5.70 | 123.4 → 122.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.14 | 3.24 | 14.63 | 99% | 0.79 | 2.50 | 5.54 | 148.8 → 144.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.13 | 3.04 | 17.74 | 99% | 0.81 | 2.31 | 5.52 | 150.8 → 147.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.15 | 3.01 | 15.83 | 99% | 0.81 | 2.30 | 5.37 | 153.7 → 150.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.07 | 3.37 | 16.64 | 100% | 0.73 | 2.69 | 6.01 | 112.7 → 111.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.12 | 3.00 | 16.75 | 99% | 0.81 | 2.29 | 5.38 | 153.1 → 150.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.11 | 2.99 | 15.41 | 99% | 0.82 | 2.23 | 4.83 | 152.6 → 149.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.70 | 2.42 | 11.41 | 99% | 0.59 | 1.88 | 4.24 | 74.3 → 73.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.427 |
| AttachingStyle - | 0.070 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| ArrangingLayoutable ScrollingTextLayer | 0.021 |
| ArrangingLayoutable LineDrawing | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.18 | 3.21 | 17.96 | 99% | 0.81 | 2.50 | 5.54 | 150.2 → 150.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.50 | 5.70 | 27.04 | 96% | 0.92 | 4.83 | 5.56 | 548.9 → 565.6 | 12 | 3.5 / 31 |

