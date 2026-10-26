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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.69 | 3.35 | 100% | 0.48 | 2.30 | 4.43 | 122.7 → 130.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.75 | 18.35 | 99% | 0.76 | 2.66 | 4.87 | 160.2 → 157.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.90 | 15.22 | 99% | 0.87 | 2.35 | 4.70 | 206.4 → 191.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.73 | 13.56 | 99% | 0.62 | 2.31 | 4.57 | 144.5 → 145.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 3.36 | 67.40 | 99% | 0.82 | 2.53 | 5.06 | 119.5 → 117.7 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.88 | 2.84 | 14.76 | 99% | 0.73 | 2.30 | 5.20 | 144.3 → 140.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.75 | 14.06 | 99% | 0.66 | 2.28 | 4.88 | 146.4 → 143.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 3.46 | 15.13 | 99% | 0.83 | 2.67 | 5.21 | 149.7 → 146.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.84 | 10.86 | 99% | 0.55 | 2.42 | 4.77 | 108.3 → 106.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 3.19 | 14.40 | 99% | 0.73 | 2.59 | 4.86 | 148.8 → 146.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.95 | 3.86 | 12.75 | 99% | 0.78 | 2.75 | 4.63 | 149.0 → 146.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.17 | 8.63 | 100% | 0.46 | 1.89 | 3.79 | 70.0 → 68.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.242 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.00 | 13.41 | 99% | 0.69 | 2.48 | 4.89 | 145.1 → 146.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.06 | 7.28 | 75.63 | 96% | 0.95 | 6.34 | 5.52 | 553.8 → 564.2 | 13 | 3.5 / 31 |

