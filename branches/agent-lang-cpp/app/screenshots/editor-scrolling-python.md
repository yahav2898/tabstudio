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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 2.88 | 12.96 | 99% | 0.62 | 2.29 | 5.28 | 120.6 → 128.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 3.60 | 83.49 | 99% | 0.92 | 2.67 | 5.68 | 157.6 → 155.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.39 | 3.32 | 18.84 | 99% | 1.10 | 2.31 | 5.77 | 203.8 → 189.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.18 | 2.98 | 16.36 | 99% | 0.83 | 2.24 | 5.45 | 142.3 → 143.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.00 | 3.09 | 13.01 | 99% | 0.93 | 2.08 | 5.48 | 117.4 → 115.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.08 | 2.88 | 13.64 | 99% | 0.83 | 2.15 | 5.48 | 141.5 → 137.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.15 | 2.88 | 16.99 | 99% | 0.85 | 2.27 | 5.57 | 143.8 → 140.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.51 | 79.98 | 99% | 0.86 | 2.64 | 5.68 | 147.5 → 143.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.96 | 2.69 | 11.48 | 99% | 0.68 | 2.02 | 5.23 | 105.9 → 104.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.12 | 2.88 | 15.95 | 99% | 0.83 | 2.16 | 5.57 | 146.5 → 143.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.17 | 4.47 | 14.53 | 99% | 0.87 | 2.78 | 4.92 | 146.7 → 143.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 2.17 | 10.55 | 100% | 0.62 | 1.68 | 4.54 | 67.9 → 67.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.338 |
| AttachingStyle - | 0.060 |
| ArrangingLayoutable TextView | 0.026 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.17 | 2.98 | 16.71 | 99% | 0.82 | 2.29 | 5.51 | 142.5 → 143.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.87 | 7.82 | 90.19 | 95% | 0.98 | 6.03 | 5.74 | 550.1 → 567.6 | 13 | 3.5 / 31 |

