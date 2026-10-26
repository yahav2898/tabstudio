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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 2.52 | 11.22 | 99% | 0.56 | 2.03 | 5.28 | 123.2 → 130.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.16 | 3.86 | 13.92 | 99% | 0.89 | 2.70 | 5.49 | 160.3 → 158.5 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 3.02 | 14.70 | 99% | 1.01 | 2.11 | 5.22 | 207.4 → 192.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 2.65 | 14.12 | 99% | 0.74 | 2.06 | 4.98 | 145.0 → 146.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.51 | 9.34 | 100% | 0.74 | 1.85 | 5.15 | 119.4 → 117.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.13 | 4.62 | 16.46 | 99% | 0.86 | 4.00 | 5.34 | 145.4 → 141.3 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 2.57 | 15.09 | 99% | 0.73 | 2.00 | 4.86 | 146.6 → 143.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 2.58 | 14.60 | 99% | 0.76 | 2.02 | 4.93 | 149.5 → 146.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.87 | 2.52 | 12.05 | 99% | 0.60 | 1.96 | 4.72 | 108.6 → 107.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 3.21 | 15.14 | 99% | 0.84 | 2.52 | 5.33 | 150.3 → 146.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 2.59 | 16.08 | 99% | 0.74 | 2.01 | 4.48 | 149.4 → 146.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.00 | 8.73 | 100% | 0.55 | 1.58 | 3.99 | 69.5 → 68.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.345 |
| ArrangingLayoutable LineDrawing | 0.048 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.06 | 4.42 | 13.87 | 99% | 1.67 | 2.73 | 5.52 | 145.8 → 146.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.46 | 5.73 | 28.11 | 96% | 0.83 | 4.84 | 5.35 | 556.0 → 575.7 | 13 | 3.5 / 31 |

