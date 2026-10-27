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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 2.40 | 12.61 | 99% | 0.51 | 1.94 | 4.97 | 128.8 → 136.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.89 | 14.90 | 99% | 0.76 | 2.22 | 5.13 | 165.0 → 163.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.23 | 4.57 | 17.00 | 99% | 2.58 | 2.74 | 5.57 | 212.6 → 196.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.88 | 2.56 | 15.50 | 99% | 0.69 | 1.99 | 4.94 | 150.2 → 151.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.35 | 11.67 | 99% | 0.67 | 1.85 | 4.88 | 125.2 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.87 | 19.56 | 99% | 0.75 | 2.06 | 5.05 | 149.7 → 146.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.05 | 3.29 | 15.04 | 99% | 0.82 | 2.65 | 5.58 | 153.1 → 148.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 2.61 | 14.30 | 99% | 0.68 | 2.07 | 5.50 | 154.6 → 151.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.33 | 13.34 | 99% | 0.55 | 1.91 | 4.71 | 114.6 → 112.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 3.22 | 13.84 | 99% | 0.82 | 2.21 | 5.07 | 154.5 → 152.4 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.22 | 12.55 | 99% | 0.72 | 2.59 | 4.79 | 155.1 → 151.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 1.98 | 11.60 | 100% | 0.49 | 1.62 | 3.97 | 75.5 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.285 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 2.62 | 14.54 | 99% | 0.68 | 2.08 | 5.01 | 150.8 → 151.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.29 | 7.29 | 23.44 | 96% | 0.91 | 6.03 | 5.78 | 556.6 → 572.6 | 12 | 3.5 / 31 |

