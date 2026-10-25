# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.58 | 14.22 | 100% | 0.68 | 1.85 | 5.07 | 88.2 → 95.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.98 | 2.47 | 13.99 | 99% | 0.81 | 1.77 | 4.73 | 124.4 → 122.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.57 | 14.70 | 99% | 0.97 | 1.69 | 4.92 | 171.2 → 156.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 2.86 | 15.61 | 99% | 0.87 | 1.85 | 5.13 | 109.6 → 110.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 2.12 | 14.00 | 99% | 0.77 | 1.52 | 4.90 | 84.4 → 82.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 2.42 | 14.45 | 99% | 0.79 | 1.68 | 5.04 | 109.4 → 105.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 2.88 | 15.64 | 99% | 0.83 | 1.86 | 5.15 | 111.6 → 108.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 2.27 | 13.34 | 99% | 0.72 | 1.70 | 4.84 | 114.4 → 110.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 2.60 | 62.18 | 99% | 0.66 | 1.83 | 5.36 | 73.6 → 71.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 2.41 | 13.99 | 99% | 0.81 | 1.79 | 5.11 | 114.1 → 110.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 2.37 | 14.99 | 100% | 0.73 | 1.76 | 4.68 | 114.2 → 110.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.00 | 14.44 | 100% | 0.57 | 1.45 | 4.24 | 35.0 → 34.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| MeasuringLayoutable LineDrawing | 0.038 |
| ArrangingLayoutable TextView | 0.020 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.49 | 13.63 | 99% | 0.76 | 1.80 | 4.92 | 110.2 → 110.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.52 | 8.62 | 25.05 | 95% | 0.92 | 6.54 | 8.93 | 603.9 → 615.2 | 14 | 3.5 / 31 |

