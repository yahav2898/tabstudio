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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.88 | 18.47 | 99% | 0.51 | 2.44 | 4.08 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.13 | 17.79 | 99% | 0.82 | 2.56 | 4.30 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 4.43 | 30.67 | 99% | 2.05 | 2.62 | 4.83 | 176.8 → 162.7 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.34 | 29.54 | 99% | 1.02 | 2.55 | 4.46 | 115.2 → 116.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.92 | 15.94 | 99% | 0.79 | 2.36 | 4.35 | 91.5 → 89.2 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 2.76 | 15.20 | 99% | 0.61 | 2.28 | 4.29 | 115.4 → 110.7 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.70 | 2.98 | 15.24 | 99% | 0.65 | 2.46 | 4.56 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.68 | 2.77 | 16.48 | 99% | 0.75 | 2.32 | 4.09 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.66 | 3.16 | 16.70 | 99% | 1.54 | 2.42 | 4.35 | 80.6 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.73 | 2.99 | 15.74 | 99% | 0.68 | 2.46 | 4.41 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 2.69 | 15.71 | 99% | 0.59 | 2.28 | 3.40 | 120.0 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.37 | 27.73 | 100% | 0.43 | 2.06 | 3.45 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.315 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.35 | 38.64 | 99% | 1.77 | 2.55 | 4.32 | 116.3 → 117.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.16 | 6.21 | 26.42 | 96% | 1.57 | 4.76 | 7.45 | 583.8 → 619.5 | 13 | 3.5 / 30 |

