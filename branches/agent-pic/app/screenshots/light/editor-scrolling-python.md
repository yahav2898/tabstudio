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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.07 | 11.18 | 99% | 0.42 | 1.75 | 4.30 | 122.2 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 3.27 | 78.99 | 99% | 0.74 | 2.43 | 4.99 | 158.0 → 156.4 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.74 | 14.53 | 99% | 0.91 | 1.91 | 4.96 | 204.5 → 189.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.16 | 13.72 | 99% | 0.58 | 1.74 | 4.55 | 143.4 → 144.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.03 | 10.81 | 100% | 0.57 | 1.64 | 4.65 | 118.3 → 116.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 3.49 | 14.44 | 99% | 0.82 | 2.08 | 5.17 | 143.2 → 139.6 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.64 | 81.34 | 99% | 0.61 | 1.93 | 4.84 | 145.2 → 141.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.20 | 15.48 | 99% | 0.59 | 1.76 | 4.76 | 147.8 → 144.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 2.02 | 12.37 | 99% | 0.48 | 1.65 | 4.53 | 107.7 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.94 | 3.30 | 16.29 | 99% | 0.73 | 2.43 | 5.21 | 147.7 → 145.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.92 | 13.44 | 99% | 0.72 | 2.09 | 4.40 | 148.2 → 144.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.67 | 14.89 | 100% | 0.44 | 1.41 | 3.84 | 68.9 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.194 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| this editor | 368 | 0.70 | 2.18 | 16.46 | 99% | 0.58 | 1.72 | 4.64 | 143.7 → 144.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.55 | 8.93 | 83.85 | 94% | 0.87 | 7.07 | 5.44 | 548.0 → 563.3 | 12 | 3.5 / 31 |

