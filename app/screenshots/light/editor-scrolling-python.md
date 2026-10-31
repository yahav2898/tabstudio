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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 2.07 | 9.68 | 100% | 0.30 | 1.84 | 2.67 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.19 | 14.19 | 99% | 0.51 | 1.89 | 2.99 | 129.3 → 129.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.59 | 2.93 | 14.47 | 99% | 1.24 | 2.09 | 3.33 | 176.1 → 162.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.51 | 2.36 | 15.14 | 99% | 0.52 | 1.96 | 3.06 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.53 | 15.31 | 99% | 0.58 | 2.07 | 3.38 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.69 | 2.79 | 16.58 | 99% | 1.16 | 2.19 | 3.43 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 4.27 | 17.20 | 99% | 2.42 | 2.42 | 3.62 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.67 | 14.71 | 99% | 0.58 | 2.12 | 3.37 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.55 | 2.41 | 12.34 | 99% | 0.54 | 2.06 | 3.06 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.65 | 2.65 | 13.96 | 99% | 1.13 | 2.15 | 3.43 | 119.5 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.56 | 2.77 | 15.27 | 99% | 0.82 | 2.19 | 3.12 | 119.7 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.24 | 10.98 | 100% | 0.55 | 1.89 | 2.51 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.226 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.02 | 38.66 | 99% | 1.49 | 2.35 | 3.50 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.84 | 7.31 | 26.69 | 96% | 0.91 | 5.18 | 6.21 | 571.7 → 597.2 | 13 | 3.5 / 30 |

