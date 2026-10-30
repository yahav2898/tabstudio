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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.02 | 3.85 | 20.32 | 99% | 0.67 | 3.19 | 5.19 | 96.4 → 103.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 4.14 | 20.42 | 99% | 1.75 | 3.22 | 5.74 | 133.1 → 130.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.53 | 5.52 | 23.36 | 99% | 2.77 | 3.30 | 6.33 | 180.7 → 166.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 3.83 | 19.79 | 99% | 0.94 | 3.19 | 5.77 | 118.1 → 119.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 3.45 | 17.85 | 100% | 0.82 | 2.88 | 5.50 | 93.0 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.88 | 3.66 | 26.96 | 99% | 1.34 | 3.07 | 5.73 | 118.1 → 113.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 3.88 | 21.37 | 99% | 1.98 | 3.14 | 5.66 | 120.2 → 116.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.03 | 5.11 | 20.59 | 99% | 2.10 | 3.17 | 5.68 | 123.2 → 119.5 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.59 | 18.18 | 99% | 0.65 | 3.01 | 5.22 | 82.2 → 80.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.89 | 3.87 | 19.50 | 99% | 1.67 | 3.10 | 5.54 | 122.8 → 119.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 3.77 | 20.55 | 99% | 0.94 | 3.09 | 4.72 | 122.9 → 119.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 3.28 | 6.96 | 100% | 1.94 | 2.77 | 4.67 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.294 |
| MeasuringLayoutable NumberDrawing | 0.073 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 3.73 | 17.80 | 99% | 0.82 | 3.07 | 5.24 | 118.8 → 119.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.84 | 9.40 | 37.87 | 93% | 2.18 | 8.16 | 9.80 | 623.2 → 646.6 | 15 | 3.5 / 31 |

