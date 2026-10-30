# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 3.68 | 18.94 | 99% | 0.66 | 3.11 | 5.11 | 95.4 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.26 | 4.01 | 22.53 | 99% | 1.76 | 3.16 | 5.92 | 131.3 → 129.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.38 | 5.18 | 19.84 | 98% | 2.12 | 3.26 | 6.09 | 178.0 → 163.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.16 | 3.88 | 19.08 | 99% | 0.90 | 3.09 | 5.86 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.61 | 17.52 | 99% | 0.92 | 2.88 | 5.56 | 92.0 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.19 | 4.93 | 89.86 | 99% | 2.62 | 3.17 | 6.13 | 116.4 → 112.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.16 | 3.91 | 19.50 | 100% | 1.04 | 3.12 | 5.67 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.14 | 3.79 | 18.94 | 99% | 1.02 | 3.10 | 5.51 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.99 | 3.68 | 11.99 | 100% | 0.80 | 3.00 | 5.46 | 81.3 → 79.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.21 | 3.98 | 25.83 | 99% | 1.34 | 3.17 | 5.69 | 121.3 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.16 | 4.00 | 20.12 | 99% | 1.02 | 3.17 | 5.13 | 121.3 → 117.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 3.35 | 22.05 | 100% | 0.69 | 2.76 | 4.72 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.333 |
| AttachingStyle - | 0.072 |
| ArrangingLayoutable TextView | 0.025 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.22 | 4.07 | 21.20 | 99% | 1.64 | 3.21 | 5.87 | 117.3 → 118.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.15 | 8.14 | 29.60 | 94% | 1.14 | 6.67 | 9.88 | 613.7 → 631.9 | 14 | 3.5 / 31 |

