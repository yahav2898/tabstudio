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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.23 | 12.79 | 99% | 0.50 | 1.78 | 4.76 | 120.0 → 127.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 3.46 | 15.87 | 99% | 0.78 | 2.63 | 5.02 | 156.5 → 154.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 2.75 | 17.15 | 99% | 0.92 | 2.16 | 4.99 | 202.8 → 188.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 2.70 | 16.38 | 99% | 0.65 | 2.27 | 4.82 | 141.6 → 142.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 2.92 | 15.30 | 99% | 0.65 | 2.44 | 4.91 | 116.6 → 115.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.89 | 69.36 | 99% | 0.71 | 2.36 | 4.75 | 141.4 → 137.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.68 | 15.19 | 99% | 0.68 | 2.19 | 4.84 | 143.4 → 139.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.94 | 17.09 | 99% | 0.63 | 2.49 | 5.05 | 146.0 → 143.2 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.90 | 10.10 | 99% | 0.55 | 2.44 | 5.32 | 106.6 → 104.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.76 | 12.90 | 99% | 0.67 | 2.30 | 4.85 | 145.8 → 142.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.58 | 14.98 | 99% | 0.63 | 2.19 | 4.43 | 145.9 → 142.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.56 | 4.77 | 100% | 0.47 | 2.22 | 3.97 | 67.3 → 67.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.316 |
| ArrangingLayoutable Track | 0.051 |
| ArrangingLayoutable TextView | 0.037 |
| AttachingStyle - | 0.034 |
| FindingResource - | 0.021 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Rectangle | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.82 | 15.59 | 99% | 0.64 | 2.36 | 4.74 | 141.9 → 142.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.51 | 7.67 | 28.86 | 95% | 0.77 | 6.60 | 6.58 | 550.4 → 571.3 | 13 | 3.5 / 31 |

