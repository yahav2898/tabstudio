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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.37 | 2.11 | 13.99 | 99% | 0.38 | 1.91 | 2.97 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.56 | 2.55 | 14.65 | 99% | 1.02 | 2.03 | 3.33 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.65 | 2.54 | 15.68 | 99% | 1.25 | 2.06 | 3.30 | 176.8 → 162.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 2.26 | 13.80 | 99% | 0.43 | 1.97 | 2.92 | 115.2 → 116.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 2.20 | 14.27 | 99% | 0.54 | 1.88 | 2.89 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.44 | 2.61 | 17.53 | 99% | 0.83 | 2.01 | 2.98 | 115.4 → 110.8 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.70 | 4.27 | 85.14 | 99% | 2.12 | 2.47 | 3.54 | 117.4 → 113.3 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.48 | 16.06 | 99% | 0.94 | 2.00 | 3.21 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.48 | 2.28 | 11.65 | 99% | 0.65 | 1.95 | 3.10 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.57 | 2.54 | 15.49 | 99% | 1.02 | 2.12 | 4.39 | 119.9 → 116.1 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.56 | 2.51 | 17.83 | 99% | 0.53 | 2.09 | 2.80 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.18 | 12.61 | 100% | 0.47 | 1.89 | 2.33 | 44.0 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.23 | 36.42 | 99% | 1.69 | 2.29 | 3.55 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.53 | 4.48 | 21.06 | 96% | 1.26 | 3.74 | 5.46 | 574.3 → 599.3 | 13 | 3.5 / 30 |

