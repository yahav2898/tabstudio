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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.24 | 12.33 | 100% | 0.46 | 1.88 | 4.42 | 122.1 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 3.09 | 21.59 | 99% | 0.89 | 2.25 | 5.02 | 158.2 → 156.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 2.46 | 17.45 | 99% | 0.91 | 1.76 | 5.00 | 204.4 → 189.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.19 | 13.99 | 99% | 0.60 | 1.75 | 4.65 | 143.4 → 144.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.01 | 11.98 | 99% | 0.58 | 1.59 | 4.74 | 118.3 → 116.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 4.02 | 17.02 | 99% | 0.76 | 2.70 | 5.42 | 143.1 → 139.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.34 | 14.25 | 99% | 0.62 | 1.86 | 4.85 | 145.1 → 141.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 2.41 | 14.75 | 99% | 0.67 | 1.93 | 4.99 | 147.9 → 144.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.17 | 10.44 | 99% | 0.49 | 1.76 | 4.23 | 107.7 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 3.68 | 13.11 | 99% | 0.85 | 2.69 | 5.44 | 148.3 → 145.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.33 | 14.36 | 99% | 0.62 | 1.83 | 4.05 | 147.5 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.80 | 11.07 | 100% | 0.44 | 1.50 | 3.93 | 68.8 → 68.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.192 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 2.93 | 15.01 | 99% | 0.69 | 2.05 | 4.68 | 143.7 → 145.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.49 | 5.72 | 31.42 | 96% | 0.83 | 4.60 | 5.56 | 547.1 → 564.0 | 13 | 3.5 / 31 |

