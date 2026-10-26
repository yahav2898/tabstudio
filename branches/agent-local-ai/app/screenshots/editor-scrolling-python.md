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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 1.99 | 10.37 | 99% | 0.42 | 1.64 | 4.26 | 122.8 → 130.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 2.82 | 14.26 | 100% | 0.72 | 1.93 | 4.78 | 159.8 → 157.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 3.62 | 14.13 | 99% | 1.08 | 1.85 | 5.14 | 207.5 → 192.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.14 | 13.82 | 99% | 0.61 | 1.73 | 4.78 | 144.9 → 146.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.21 | 13.10 | 99% | 0.65 | 1.74 | 4.77 | 119.4 → 117.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.93 | 3.45 | 72.94 | 99% | 0.92 | 2.35 | 5.66 | 144.5 → 141.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.13 | 13.44 | 99% | 0.60 | 1.65 | 4.63 | 146.8 → 143.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.13 | 13.39 | 99% | 0.61 | 1.68 | 4.62 | 149.4 → 146.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 1.94 | 13.32 | 99% | 0.45 | 1.60 | 4.37 | 108.4 → 106.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 3.00 | 11.12 | 99% | 0.69 | 1.96 | 4.71 | 149.1 → 146.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.93 | 11.80 | 99% | 0.63 | 2.43 | 4.37 | 149.4 → 145.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.63 | 12.98 | 100% | 0.42 | 1.33 | 3.75 | 69.2 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.192 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.13 | 13.77 | 99% | 0.61 | 1.67 | 4.62 | 145.2 → 146.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.99 | 8.68 | 80.57 | 93% | 2.53 | 6.14 | 5.69 | 558.1 → 571.8 | 12 | 3.5 / 31 |

