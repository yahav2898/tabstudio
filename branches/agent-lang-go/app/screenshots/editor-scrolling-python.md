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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 3.32 | 12.36 | 100% | 0.69 | 2.54 | 3.25 | 121.7 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.47 | 1.49 | 11.97 | 99% | 0.48 | 1.21 | 3.00 | 158.4 → 155.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.53 | 1.59 | 13.00 | 99% | 0.54 | 1.16 | 2.97 | 205.3 → 190.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.45 | 1.90 | 12.87 | 99% | 0.48 | 1.61 | 3.00 | 143.4 → 145.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.85 | 8.93 | 100% | 0.45 | 1.58 | 2.95 | 118.1 → 116.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 1.96 | 13.27 | 99% | 0.54 | 1.59 | 3.14 | 143.3 → 139.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 3.35 | 14.08 | 99% | 2.38 | 2.34 | 3.77 | 145.8 → 143.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 2.99 | 73.80 | 99% | 0.63 | 1.76 | 3.56 | 148.8 → 145.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.73 | 2.17 | 10.40 | 99% | 0.57 | 1.64 | 3.51 | 107.5 → 106.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 1.72 | 13.30 | 99% | 0.52 | 1.33 | 3.11 | 147.9 → 144.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 1.89 | 11.20 | 99% | 0.49 | 1.42 | 2.86 | 147.8 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.39 | 8.14 | 99% | 0.38 | 1.13 | 2.44 | 68.3 → 67.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.208 |
| AttachingStyle - | 0.050 |
| ArrangingLayoutable RepeatButton | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.95 | 15.24 | 99% | 0.83 | 2.60 | 3.55 | 144.7 → 145.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 4.36 | 21.42 | 97% | 0.63 | 3.13 | 3.55 | 547.7 → 569.3 | 12 | 3.5 / 31 |

