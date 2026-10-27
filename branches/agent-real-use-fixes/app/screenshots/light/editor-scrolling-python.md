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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.12 | 11.01 | 99% | 0.43 | 1.84 | 4.45 | 130.8 → 138.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.42 | 14.62 | 99% | 0.66 | 1.87 | 5.22 | 167.1 → 164.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 2.91 | 16.26 | 99% | 0.86 | 2.54 | 5.36 | 213.7 → 199.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.93 | 4.83 | 90.22 | 99% | 2.47 | 4.15 | 5.63 | 153.6 → 155.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.92 | 12.00 | 99% | 0.63 | 2.51 | 5.18 | 127.5 → 126.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 3.01 | 18.93 | 99% | 0.62 | 2.61 | 5.22 | 151.9 → 148.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 3.01 | 16.25 | 99% | 0.62 | 2.62 | 4.94 | 154.1 → 151.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 3.05 | 15.78 | 99% | 0.60 | 2.62 | 5.07 | 157.0 → 154.3 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.80 | 3.39 | 10.37 | 99% | 0.59 | 2.85 | 4.99 | 117.3 → 115.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 2.90 | 21.28 | 99% | 0.63 | 2.59 | 4.98 | 156.7 → 154.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.91 | 16.07 | 99% | 0.62 | 2.51 | 4.34 | 156.8 → 153.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.36 | 12.75 | 100% | 0.42 | 2.26 | 4.26 | 78.8 → 78.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.199 |
| AttachingStyle - | 0.053 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 4.61 | 16.08 | 99% | 0.77 | 2.90 | 5.10 | 153.2 → 155.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.18 | 5.86 | 24.79 | 96% | 0.74 | 4.88 | 5.27 | 553.6 → 569.3 | 12 | 3.5 / 31 |

