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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.24 | 14.53 | 100% | 0.51 | 2.83 | 4.68 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 3.57 | 17.64 | 99% | 1.04 | 2.89 | 5.05 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 4.81 | 17.87 | 99% | 1.94 | 2.99 | 5.68 | 176.2 → 162.2 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 3.92 | 99.42 | 99% | 2.00 | 3.05 | 5.85 | 114.9 → 115.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.45 | 17.89 | 100% | 0.78 | 2.75 | 5.55 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.84 | 3.36 | 27.48 | 99% | 0.76 | 2.82 | 5.41 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.81 | 3.42 | 17.67 | 99% | 1.31 | 2.82 | 5.45 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.02 | 5.03 | 21.20 | 99% | 2.55 | 3.03 | 5.73 | 119.9 → 115.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.65 | 3.08 | 12.69 | 99% | 0.52 | 2.63 | 4.62 | 80.2 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.74 | 3.21 | 16.39 | 99% | 0.74 | 2.70 | 4.91 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 3.18 | 16.50 | 99% | 0.87 | 2.70 | 4.09 | 119.7 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.78 | 18.32 | 100% | 0.50 | 2.43 | 4.19 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.397 |
| ArrangingLayoutable ScrollingTextLayer | 0.092 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable TextView | 0.032 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 3.30 | 38.40 | 99% | 0.68 | 2.76 | 4.88 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.39 | 8.40 | 31.25 | 94% | 1.79 | 7.99 | 9.51 | 597.3 → 626.3 | 15 | 3.5 / 30 |

