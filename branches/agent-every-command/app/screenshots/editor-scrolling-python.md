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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 3.17 | 26.39 | 99% | 0.63 | 2.58 | 5.75 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 3.39 | 25.12 | 100% | 1.15 | 2.66 | 6.88 | 129.8 → 130.2 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.48 | 5.17 | 100.90 | 99% | 2.57 | 2.76 | 7.28 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 3.29 | 22.31 | 99% | 0.95 | 2.51 | 5.83 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 2.96 | 24.89 | 100% | 0.90 | 2.31 | 6.58 | 91.6 → 89.3 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 1.07 | 3.17 | 30.54 | 99% | 0.90 | 2.46 | 6.82 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.20 | 4.18 | 25.14 | 99% | 1.81 | 2.66 | 6.90 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.10 | 3.25 | 24.72 | 100% | 0.95 | 2.49 | 6.78 | 120.3 → 116.2 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.94 | 3.01 | 22.46 | 99% | 0.69 | 2.42 | 5.68 | 80.8 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.12 | 3.21 | 23.76 | 100% | 0.94 | 2.45 | 5.82 | 120.0 → 116.2 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.14 | 4.20 | 26.65 | 100% | 1.72 | 2.64 | 6.19 | 120.1 → 116.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.77 | 12.88 | 99% | 1.39 | 2.21 | 5.69 | 44.0 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.327 |
| ArrangingLayoutable ScrollingTextLayer | 0.095 |
| AttachingStyle - | 0.024 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.17 | 3.26 | 45.34 | 99% | 0.95 | 2.52 | 7.04 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.83 | 10.81 | 39.29 | 94% | 1.91 | 8.56 | 11.84 | 612.0 → 630.9 | 10 | 3.5 / 30 |

