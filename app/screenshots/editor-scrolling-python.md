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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 3.00 | 13.28 | 100% | 0.46 | 2.63 | 6.20 | 93.5 → 100.7 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.18 | 16.75 | 99% | 0.87 | 2.66 | 6.67 | 129.8 → 130.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 5.45 | 104.62 | 99% | 2.86 | 2.88 | 7.62 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.17 | 18.83 | 99% | 0.63 | 2.70 | 6.80 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.01 | 14.90 | 99% | 0.78 | 2.53 | 6.85 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 3.26 | 27.15 | 99% | 1.56 | 2.72 | 7.04 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.82 | 3.52 | 17.89 | 99% | 1.57 | 2.77 | 7.14 | 117.6 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.25 | 16.77 | 99% | 0.83 | 2.71 | 7.09 | 120.3 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 2.92 | 13.60 | 100% | 0.47 | 2.58 | 6.09 | 80.8 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.85 | 3.79 | 19.44 | 99% | 1.63 | 2.81 | 7.22 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.77 | 3.24 | 23.51 | 99% | 1.24 | 2.62 | 6.44 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.67 | 12.84 | 100% | 0.47 | 2.34 | 5.94 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.85 | 36.08 | 99% | 1.62 | 2.78 | 7.20 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.16 | 10.46 | 26.26 | 95% | 1.46 | 5.92 | 12.74 | 605.6 → 632.9 | 14 | 3.5 / 30 |

