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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 2.42 | 14.96 | 99% | 0.46 | 2.02 | 3.04 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 2.62 | 14.47 | 99% | 1.07 | 2.07 | 3.29 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.01 | 16.61 | 99% | 1.58 | 2.31 | 3.91 | 176.9 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.48 | 13.94 | 99% | 0.54 | 2.04 | 3.08 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.60 | 15.17 | 99% | 0.65 | 2.08 | 3.34 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.84 | 3.23 | 18.23 | 99% | 1.63 | 2.41 | 3.70 | 115.4 → 110.8 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.58 | 2.63 | 19.60 | 99% | 1.09 | 2.19 | 3.13 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.89 | 15.57 | 99% | 0.64 | 2.28 | 3.67 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 2.64 | 13.25 | 99% | 0.64 | 2.16 | 3.12 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.61 | 2.70 | 17.51 | 99% | 0.66 | 2.18 | 3.40 | 119.9 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.68 | 2.80 | 14.88 | 99% | 0.64 | 2.27 | 2.88 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.58 | 14.08 | 100% | 0.60 | 2.09 | 2.77 | 44.0 → 43.0 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.360 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.037 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.77 | 40.71 | 99% | 0.64 | 2.31 | 3.37 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.74 | 6.15 | 37.20 | 95% | 0.88 | 4.57 | 6.32 | 577.8 → 601.2 | 13 | 3.5 / 30 |

