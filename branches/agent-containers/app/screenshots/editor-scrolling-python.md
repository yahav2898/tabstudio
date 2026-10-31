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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.43 | 2.23 | 15.43 | 100% | 0.35 | 1.93 | 2.70 | 93.4 → 100.7 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 2.19 | 14.79 | 99% | 0.72 | 1.88 | 2.86 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 2.30 | 14.25 | 99% | 1.02 | 1.90 | 3.05 | 176.9 → 162.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.42 | 2.19 | 14.17 | 99% | 0.49 | 1.90 | 2.91 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 2.21 | 15.30 | 99% | 0.50 | 1.89 | 2.86 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.47 | 2.32 | 15.79 | 99% | 0.55 | 1.97 | 2.96 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.48 | 2.37 | 16.27 | 99% | 0.51 | 2.05 | 2.88 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.52 | 2.36 | 16.01 | 99% | 0.54 | 2.00 | 3.09 | 120.3 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.47 | 2.68 | 13.39 | 99% | 1.36 | 2.10 | 3.13 | 80.7 → 78.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.63 | 3.77 | 15.77 | 99% | 2.35 | 2.30 | 3.27 | 120.0 → 116.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.43 | 2.28 | 16.38 | 99% | 0.47 | 1.99 | 2.70 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.93 | 12.32 | 100% | 0.33 | 1.76 | 2.41 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.215 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.46 | 2.24 | 30.65 | 99% | 0.49 | 1.96 | 2.96 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 4.79 | 22.92 | 96% | 0.84 | 4.03 | 5.38 | 572.5 → 602.3 | 14 | 3.5 / 30 |

