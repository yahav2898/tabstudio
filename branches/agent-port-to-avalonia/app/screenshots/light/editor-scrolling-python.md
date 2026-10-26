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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 1.54 | 10.55 | 99% | 0.37 | 1.28 | 3.52 | 121.8 → 129.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 1.90 | 14.15 | 99% | 0.57 | 1.43 | 3.90 | 158.8 → 156.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 2.03 | 14.25 | 99% | 0.78 | 1.52 | 4.02 | 205.8 → 191.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 1.77 | 13.78 | 99% | 0.50 | 1.36 | 3.77 | 143.7 → 144.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.47 | 12.52 | 99% | 0.60 | 1.52 | 3.80 | 118.0 → 117.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.66 | 3.17 | 13.04 | 99% | 0.62 | 2.44 | 3.99 | 143.9 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 1.61 | 12.82 | 99% | 0.49 | 1.27 | 3.77 | 145.2 → 141.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 1.82 | 13.78 | 99% | 0.56 | 1.46 | 3.88 | 148.2 → 145.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.64 | 10.04 | 100% | 0.40 | 1.30 | 3.94 | 107.1 → 105.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.12 | 15.89 | 99% | 0.57 | 1.60 | 3.96 | 148.4 → 145.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 3.00 | 75.25 | 99% | 0.65 | 2.42 | 3.62 | 148.4 → 144.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.47 | 8.10 | 100% | 0.39 | 1.13 | 3.28 | 68.4 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.84 | 14.34 | 99% | 0.54 | 1.45 | 3.90 | 144.2 → 144.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.68 | 3.94 | 22.05 | 96% | 0.62 | 3.36 | 4.17 | 546.3 → 563.2 | 13 | 3.5 / 31 |

