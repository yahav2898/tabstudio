# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.49 | 14.07 | 99% | 0.58 | 1.96 | 4.37 | 129.0 → 136.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 2.69 | 15.74 | 99% | 0.72 | 2.00 | 4.47 | 165.2 → 163.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 3.04 | 19.47 | 99% | 0.96 | 2.19 | 4.65 | 212.0 → 197.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 4.51 | 78.36 | 99% | 0.83 | 2.67 | 4.77 | 151.4 → 152.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 2.42 | 16.40 | 99% | 0.68 | 1.84 | 4.37 | 125.5 → 123.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.83 | 17.66 | 99% | 0.69 | 2.30 | 4.40 | 149.9 → 146.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.89 | 2.82 | 16.98 | 99% | 0.61 | 2.34 | 4.36 | 152.1 → 149.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.00 | 3.38 | 17.73 | 99% | 0.81 | 2.78 | 4.58 | 155.4 → 152.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 3.26 | 80.50 | 99% | 0.62 | 2.69 | 4.34 | 115.1 → 113.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 3.48 | 16.11 | 99% | 0.76 | 2.78 | 4.59 | 155.2 → 152.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.06 | 3.11 | 15.21 | 99% | 0.73 | 2.55 | 4.03 | 155.2 → 151.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 2.63 | 11.95 | 100% | 0.61 | 2.29 | 3.71 | 76.9 → 76.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.396 |
| ArrangingLayoutable TextView | 0.036 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable PageHost | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.12 | 3.57 | 15.49 | 99% | 0.80 | 2.86 | 4.72 | 152.0 → 152.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.31 | 5.77 | 26.06 | 96% | 0.90 | 4.70 | 4.60 | 554.2 → 578.1 | 12 | 3.5 / 31 |

