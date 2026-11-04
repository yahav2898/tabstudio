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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 3.32 | 16.01 | 99% | 0.56 | 2.86 | 4.54 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 4.78 | 85.40 | 99% | 1.81 | 3.03 | 5.53 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 4.25 | 17.35 | 98% | 1.74 | 2.96 | 5.68 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.38 | 13.24 | 99% | 0.76 | 2.78 | 4.68 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.14 | 24.34 | 99% | 0.76 | 2.59 | 5.08 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.91 | 3.52 | 79.01 | 99% | 1.65 | 2.85 | 5.45 | 115.0 → 110.5 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.92 | 3.54 | 15.17 | 100% | 0.82 | 2.88 | 5.69 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.92 | 3.47 | 17.91 | 99% | 0.85 | 2.88 | 5.14 | 119.9 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 3.38 | 13.35 | 100% | 1.41 | 2.83 | 5.12 | 80.3 → 78.0 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.94 | 3.70 | 19.19 | 99% | 1.61 | 2.96 | 5.34 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.90 | 3.53 | 20.20 | 99% | 0.89 | 2.90 | 4.68 | 119.6 → 115.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 3.10 | 18.24 | 100% | 0.62 | 2.61 | 4.50 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.416 |
| AttachingStyle - | 0.108 |
| ArrangingLayoutable CachedLineNumberMargin | 0.043 |
| ArrangingLayoutable TextView | 0.034 |
| ArrangingLayoutable LineDrawing | 0.023 |
| FindingResource - | 0.016 |
| ArrangingLayoutable ScrollContentPresenter | 0.014 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.64 | 32.79 | 99% | 0.80 | 2.99 | 4.87 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.79 | 10.85 | 37.17 | 91% | 2.62 | 7.69 | 9.69 | 599.4 → 627.0 | 15 | 3.5 / 30 |

