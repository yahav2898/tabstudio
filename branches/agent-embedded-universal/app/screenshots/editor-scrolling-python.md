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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 3.21 | 14.43 | 99% | 0.57 | 2.73 | 5.09 | 130.0 → 136.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.98 | 3.40 | 15.90 | 99% | 0.76 | 2.71 | 5.16 | 166.0 → 163.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.25 | 5.30 | 20.44 | 99% | 2.50 | 3.04 | 5.65 | 213.7 → 198.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.95 | 16.84 | 99% | 0.63 | 2.53 | 4.93 | 150.6 → 151.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.56 | 11.09 | 99% | 0.59 | 2.22 | 4.84 | 125.4 → 123.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.97 | 12.61 | 99% | 0.60 | 2.44 | 4.96 | 149.9 → 146.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 4.01 | 87.74 | 99% | 0.83 | 2.83 | 5.29 | 153.4 → 148.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.91 | 19.04 | 99% | 0.65 | 2.52 | 5.14 | 155.0 → 152.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.68 | 12.20 | 99% | 0.46 | 2.37 | 4.53 | 114.8 → 113.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.90 | 13.97 | 99% | 0.62 | 2.39 | 4.79 | 154.7 → 152.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.95 | 3.51 | 15.46 | 99% | 0.78 | 2.80 | 4.67 | 155.4 → 152.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.35 | 6.53 | 100% | 0.45 | 2.09 | 4.17 | 75.9 → 75.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.244 |
| AttachingStyle - | 0.053 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.97 | 15.14 | 99% | 0.63 | 2.45 | 4.71 | 151.0 → 151.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.33 | 8.22 | 35.47 | 95% | 2.45 | 6.94 | 6.03 | 562.1 → 577.8 | 14 | 3.5 / 31 |

