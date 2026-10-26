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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 2.24 | 12.39 | 99% | 0.53 | 1.75 | 4.09 | 122.1 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 2.41 | 15.25 | 99% | 0.69 | 1.78 | 4.33 | 158.2 → 156.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.21 | 3.96 | 18.64 | 99% | 1.18 | 2.54 | 4.74 | 205.4 → 190.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 2.96 | 15.71 | 99% | 0.76 | 2.10 | 4.35 | 143.9 → 145.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 2.45 | 11.92 | 99% | 0.74 | 1.84 | 4.42 | 118.5 → 117.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.05 | 2.67 | 15.88 | 99% | 0.80 | 1.99 | 4.74 | 143.3 → 139.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.08 | 2.73 | 15.25 | 99% | 0.79 | 2.03 | 4.63 | 145.5 → 142.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.14 | 3.43 | 101.03 | 99% | 0.83 | 2.63 | 4.80 | 149.2 → 145.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 2.22 | 11.73 | 99% | 0.57 | 1.76 | 4.12 | 108.1 → 106.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 2.26 | 17.89 | 99% | 0.70 | 1.77 | 4.33 | 148.0 → 145.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.31 | 17.09 | 99% | 0.67 | 1.77 | 4.02 | 147.9 → 144.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.10 | 8.29 | 100% | 0.54 | 1.61 | 3.63 | 69.2 → 68.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.344 |
| ArrangingLayoutable TextView | 0.071 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable Track | 0.017 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable TextArea | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.41 | 15.44 | 99% | 0.64 | 1.79 | 4.30 | 144.1 → 144.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.89 | 4.60 | 24.22 | 96% | 0.67 | 4.11 | 4.43 | 548.1 → 568.7 | 12 | 3.5 / 31 |

