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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.89 | 15.74 | 99% | 0.51 | 2.42 | 3.96 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 3.00 | 16.23 | 99% | 0.79 | 2.46 | 4.17 | 129.2 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 3.00 | 15.57 | 99% | 1.69 | 2.40 | 4.45 | 176.1 → 162.0 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.99 | 15.25 | 99% | 0.67 | 2.43 | 4.10 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.08 | 16.57 | 99% | 1.78 | 2.40 | 4.53 | 91.1 → 88.9 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.72 | 2.96 | 18.18 | 99% | 1.39 | 2.39 | 4.36 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.80 | 3.04 | 17.61 | 99% | 0.81 | 2.49 | 4.65 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.73 | 3.17 | 24.29 | 99% | 0.72 | 2.48 | 4.83 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.59 | 2.66 | 17.66 | 99% | 0.50 | 2.26 | 4.15 | 80.2 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.15 | 16.67 | 99% | 0.77 | 2.47 | 4.46 | 119.5 → 115.8 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.89 | 4.47 | 98.37 | 99% | 2.31 | 2.64 | 4.38 | 119.6 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.64 | 14.80 | 100% | 0.49 | 2.25 | 3.53 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.291 |
| ArrangingLayoutable CachedLineNumberMargin | 0.079 |
| AttachingStyle - | 0.063 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 3.15 | 41.06 | 99% | 0.70 | 2.56 | 4.09 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.12 | 9.18 | 37.23 | 94% | 2.64 | 7.30 | 8.29 | 582.5 → 624.2 | 15 | 3.5 / 30 |

