# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 2.75 | 11.58 | 99% | 0.62 | 2.23 | 4.96 | 125.6 → 133.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 3.47 | 13.99 | 99% | 0.92 | 2.37 | 5.46 | 163.3 → 160.6 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 3.18 | 15.47 | 99% | 1.13 | 2.29 | 5.42 | 210.5 → 195.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 2.83 | 14.06 | 99% | 0.82 | 2.15 | 5.05 | 147.7 → 148.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 3.69 | 80.48 | 99% | 0.95 | 2.45 | 5.67 | 122.1 → 121.1 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.07 | 2.81 | 13.92 | 99% | 0.83 | 2.16 | 5.11 | 147.0 → 143.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 2.99 | 15.72 | 99% | 0.84 | 2.28 | 5.04 | 149.4 → 146.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.37 | 24.24 | 99% | 0.91 | 2.40 | 5.84 | 152.7 → 149.6 | 5 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 2.85 | 80.90 | 99% | 0.67 | 2.16 | 5.08 | 111.2 → 109.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 2.98 | 16.76 | 99% | 0.84 | 2.23 | 5.27 | 152.2 → 149.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.24 | 3.08 | 13.24 | 99% | 0.88 | 2.29 | 5.46 | 152.2 → 148.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.79 | 2.84 | 12.00 | 99% | 0.64 | 2.33 | 4.50 | 72.7 → 72.2 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.342 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
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
| this editor | 368 | 1.18 | 2.90 | 15.78 | 99% | 0.83 | 2.21 | 5.22 | 148.3 → 149.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.76 | 9.38 | 87.97 | 93% | 1.26 | 8.43 | 6.87 | 554.6 → 584.8 | 14 | 3.5 / 31 |

