# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.97 | 3.60 | 19.16 | 100% | 0.62 | 3.07 | 5.50 | 95.4 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.23 | 4.19 | 20.12 | 99% | 1.91 | 3.08 | 5.80 | 131.3 → 129.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.37 | 4.10 | 19.68 | 99% | 1.78 | 3.14 | 5.97 | 178.0 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 3.85 | 18.06 | 100% | 0.91 | 3.09 | 5.84 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 4.20 | 19.37 | 99% | 1.70 | 2.92 | 5.87 | 91.9 → 90.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.97 | 4.18 | 18.24 | 99% | 1.15 | 3.01 | 6.04 | 116.5 → 112.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 3.60 | 20.22 | 99% | 0.87 | 2.96 | 5.85 | 118.6 → 115.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 3.77 | 18.26 | 99% | 1.17 | 3.06 | 5.64 | 121.5 → 118.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 3.63 | 82.11 | 99% | 1.30 | 3.02 | 5.49 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 3.71 | 17.43 | 99% | 0.91 | 2.98 | 5.54 | 121.2 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 3.53 | 17.16 | 100% | 0.92 | 2.92 | 4.89 | 121.3 → 117.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 3.07 | 17.91 | 100% | 1.39 | 2.68 | 4.63 | 43.3 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.386 |
| AttachingStyle - | 0.092 |
| ArrangingLayoutable NumberDrawing | 0.058 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable ContentPresenter | 0.021 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 3.66 | 18.61 | 99% | 0.86 | 3.02 | 5.72 | 117.3 → 118.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.66 | 11.45 | 30.15 | 89% | 1.79 | 9.20 | 9.99 | 614.1 → 638.6 | 15 | 3.5 / 31 |

