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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.17 | 14.59 | 99% | 0.47 | 1.79 | 4.63 | 128.8 → 136.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.43 | 14.66 | 99% | 0.67 | 1.90 | 4.84 | 165.2 → 163.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 4.62 | 18.24 | 99% | 2.63 | 2.75 | 5.79 | 212.9 → 198.6 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 2.39 | 12.18 | 99% | 0.63 | 1.89 | 4.74 | 150.7 → 151.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.83 | 15.54 | 99% | 0.62 | 2.42 | 4.97 | 125.4 → 123.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.92 | 16.10 | 99% | 0.63 | 2.53 | 4.83 | 150.1 → 146.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 3.56 | 16.99 | 99% | 0.89 | 2.77 | 5.43 | 152.9 → 150.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.86 | 12.35 | 99% | 0.64 | 2.45 | 4.78 | 155.4 → 151.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.84 | 8.77 | 99% | 0.52 | 2.46 | 4.71 | 114.9 → 113.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.95 | 13.93 | 99% | 0.66 | 2.54 | 5.01 | 154.9 → 151.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 4.17 | 17.36 | 99% | 0.80 | 2.61 | 4.82 | 155.0 → 152.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.08 | 7.88 | 100% | 0.55 | 1.59 | 4.81 | 76.0 → 75.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.230 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 2.31 | 14.66 | 99% | 0.64 | 1.86 | 4.91 | 150.8 → 151.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.65 | 7.31 | 31.96 | 96% | 1.00 | 6.21 | 5.41 | 557.9 → 577.8 | 14 | 3.5 / 31 |

