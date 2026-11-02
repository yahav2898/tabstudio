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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 3.39 | 17.79 | 99% | 0.60 | 2.86 | 4.77 | 93.3 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.64 | 19.69 | 99% | 0.95 | 2.87 | 5.03 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 5.21 | 22.13 | 99% | 2.38 | 3.14 | 6.15 | 176.2 → 162.1 | 5 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.00 | 4.11 | 18.24 | 99% | 1.67 | 3.08 | 5.73 | 114.9 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.30 | 17.31 | 99% | 0.81 | 2.71 | 5.18 | 91.1 → 89.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.85 | 3.52 | 31.05 | 99% | 0.87 | 2.84 | 5.14 | 115.1 → 110.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.88 | 3.67 | 17.59 | 99% | 0.78 | 2.98 | 5.62 | 117.1 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.06 | 5.45 | 21.13 | 99% | 2.52 | 3.24 | 5.71 | 119.9 → 115.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.71 | 3.30 | 12.37 | 100% | 0.59 | 2.80 | 4.85 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.87 | 3.52 | 19.65 | 99% | 1.25 | 2.89 | 4.82 | 119.5 → 115.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.88 | 3.55 | 16.06 | 99% | 0.84 | 2.91 | 4.33 | 119.7 → 115.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.98 | 4.32 | 100% | 0.59 | 2.56 | 4.30 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.494 |
| AttachingStyle - | 0.095 |
| ArrangingLayoutable CachedLineNumberMargin | 0.022 |
| MeasuringLayoutable NumberDrawing | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.015 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| MeasuringLayoutable Panel | 0.011 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.50 | 42.61 | 99% | 0.83 | 2.88 | 4.86 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.49 | 8.27 | 34.98 | 94% | 1.88 | 7.28 | 9.78 | 599.4 → 624.2 | 15 | 3.5 / 30 |

