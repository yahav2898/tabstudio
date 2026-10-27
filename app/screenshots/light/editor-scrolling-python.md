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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 2.80 | 13.38 | 99% | 0.57 | 2.26 | 5.25 | 131.4 → 138.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.22 | 3.31 | 19.82 | 99% | 0.87 | 2.41 | 5.43 | 167.9 → 166.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 4.97 | 93.28 | 98% | 1.41 | 2.75 | 5.76 | 214.7 → 199.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 2.76 | 13.39 | 99% | 0.70 | 2.14 | 5.13 | 153.0 → 154.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.51 | 12.03 | 99% | 0.73 | 1.98 | 5.08 | 127.3 → 126.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.32 | 18.21 | 99% | 0.92 | 2.37 | 5.39 | 152.6 → 149.6 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.11 | 3.69 | 16.44 | 99% | 0.88 | 2.94 | 5.68 | 155.8 → 151.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 3.46 | 23.04 | 99% | 0.79 | 2.93 | 5.53 | 158.3 → 155.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.98 | 3.28 | 12.04 | 99% | 0.66 | 2.77 | 5.04 | 117.5 → 115.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.33 | 4.72 | 16.97 | 99% | 1.34 | 3.11 | 5.84 | 158.2 → 156.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.98 | 3.32 | 14.51 | 99% | 0.73 | 2.73 | 4.59 | 157.0 → 154.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 2.77 | 7.98 | 100% | 0.52 | 2.43 | 4.08 | 78.6 → 78.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| ArrangingLayoutable TextView | 0.066 |
| ArrangingLayoutable ScrollingTextLayer | 0.059 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
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
| this editor | 368 | 1.08 | 3.61 | 17.97 | 99% | 0.81 | 2.86 | 5.45 | 153.6 → 155.7 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.76 | 6.86 | 24.69 | 96% | 0.87 | 6.02 | 5.50 | 555.0 → 576.4 | 12 | 3.5 / 31 |

