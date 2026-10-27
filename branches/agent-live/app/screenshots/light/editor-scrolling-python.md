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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.32 | 11.66 | 99% | 0.49 | 1.87 | 4.49 | 128.6 → 136.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 2.76 | 13.69 | 99% | 0.69 | 2.09 | 5.09 | 164.9 → 162.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 4.34 | 20.16 | 99% | 2.27 | 2.65 | 5.52 | 212.6 → 196.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 2.48 | 14.02 | 99% | 0.66 | 1.97 | 4.82 | 150.2 → 151.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.31 | 13.77 | 99% | 0.68 | 1.78 | 5.05 | 125.0 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.43 | 16.37 | 99% | 0.67 | 1.93 | 4.78 | 149.7 → 146.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.96 | 4.35 | 84.55 | 99% | 0.77 | 2.83 | 5.10 | 152.8 → 149.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 2.48 | 14.37 | 99% | 0.70 | 1.98 | 4.99 | 154.7 → 151.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.25 | 11.00 | 99% | 0.54 | 1.84 | 4.35 | 114.4 → 112.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 2.52 | 13.93 | 99% | 0.69 | 2.01 | 4.89 | 154.5 → 151.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 3.59 | 79.79 | 99% | 0.83 | 2.62 | 4.72 | 154.6 → 152.1 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.08 | 7.33 | 100% | 0.50 | 1.67 | 4.08 | 75.6 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 2.72 | 14.95 | 99% | 0.70 | 2.09 | 5.21 | 150.8 → 151.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.35 | 8.13 | 85.89 | 95% | 2.32 | 6.49 | 6.99 | 557.3 → 574.3 | 13 | 3.5 / 31 |

