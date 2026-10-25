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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 1.54 | 8.83 | 100% | 0.34 | 1.29 | 3.42 | 121.9 → 129.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 1.71 | 13.15 | 99% | 0.53 | 1.30 | 3.81 | 158.8 → 156.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.69 | 13.74 | 99% | 0.78 | 1.65 | 3.96 | 206.1 → 191.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 3.37 | 15.15 | 99% | 0.64 | 2.50 | 4.40 | 144.9 → 145.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.54 | 12.00 | 100% | 0.48 | 1.18 | 3.72 | 118.5 → 116.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 1.61 | 11.44 | 99% | 0.49 | 1.27 | 3.82 | 143.4 → 139.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 1.69 | 12.46 | 99% | 0.49 | 1.27 | 3.70 | 145.6 → 142.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.10 | 11.79 | 99% | 0.52 | 1.46 | 3.81 | 148.4 → 145.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.92 | 9.30 | 99% | 0.52 | 2.36 | 4.00 | 108.2 → 105.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.56 | 1.61 | 10.94 | 100% | 0.49 | 1.26 | 3.83 | 148.2 → 145.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.53 | 1.57 | 12.65 | 99% | 0.49 | 1.25 | 3.36 | 148.2 → 144.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.22 | 10.26 | 100% | 0.35 | 1.01 | 3.16 | 68.4 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.158 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.86 | 12.83 | 99% | 0.58 | 1.72 | 4.07 | 144.4 → 145.8 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 3.80 | 27.99 | 96% | 0.55 | 3.20 | 3.99 | 549.4 → 567.0 | 13 | 3.5 / 31 |

