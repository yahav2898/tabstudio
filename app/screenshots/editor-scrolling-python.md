# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.76 | 18.27 | 99% | 0.39 | 2.49 | 4.07 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.12 | 18.39 | 99% | 1.27 | 2.58 | 4.49 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 3.40 | 20.03 | 99% | 1.46 | 2.62 | 5.16 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.78 | 19.25 | 99% | 1.49 | 2.73 | 4.70 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 3.07 | 16.55 | 99% | 1.20 | 2.63 | 4.92 | 91.6 → 89.2 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 2.93 | 16.82 | 99% | 0.68 | 2.52 | 4.36 | 115.5 → 110.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.62 | 2.99 | 19.35 | 99% | 1.18 | 2.58 | 4.69 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.99 | 17.63 | 99% | 0.66 | 2.53 | 4.38 | 120.2 → 116.1 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 3.28 | 14.79 | 99% | 1.37 | 2.68 | 4.77 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.73 | 3.27 | 17.66 | 99% | 1.19 | 2.73 | 4.97 | 120.0 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 3.10 | 17.69 | 99% | 0.66 | 2.61 | 4.54 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 2.56 | 10.24 | 100% | 0.44 | 2.27 | 4.02 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| ArrangingLayoutable CachedLineNumberMargin | 0.098 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.50 | 97.35 | 99% | 1.53 | 2.90 | 5.08 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.79 | 6.83 | 32.90 | 96% | 0.85 | 5.70 | 8.24 | 602.1 → 629.8 | 14 | 3.5 / 30 |

