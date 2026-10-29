# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.89 | 14.41 | 99% | 0.42 | 2.53 | 5.12 | 135.5 → 140.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.98 | 15.05 | 99% | 0.56 | 2.49 | 5.33 | 170.2 → 168.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 5.55 | 80.86 | 98% | 2.72 | 4.75 | 5.77 | 217.2 → 202.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.84 | 17.21 | 99% | 0.52 | 2.47 | 5.03 | 156.3 → 156.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.68 | 13.61 | 99% | 0.49 | 2.29 | 4.79 | 129.6 → 127.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.75 | 17.65 | 99% | 0.53 | 2.37 | 4.88 | 153.7 → 150.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.88 | 3.74 | 15.59 | 99% | 0.64 | 2.86 | 5.41 | 156.6 → 154.9 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 3.30 | 13.30 | 99% | 0.59 | 2.66 | 5.29 | 161.0 → 156.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.96 | 15.11 | 99% | 0.45 | 2.57 | 4.95 | 118.8 → 118.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 3.21 | 15.37 | 99% | 0.62 | 2.68 | 5.41 | 159.3 → 157.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.00 | 4.79 | 16.98 | 99% | 0.78 | 2.95 | 5.22 | 160.9 → 157.2 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.99 | 79.29 | 99% | 0.51 | 2.50 | 4.58 | 81.0 → 80.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.226 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 3.21 | 17.09 | 99% | 0.64 | 2.72 | 5.42 | 156.3 → 157.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.67 | 7.68 | 31.28 | 96% | 0.81 | 6.45 | 6.38 | 555.7 → 580.2 | 13 | 3.5 / 31 |

