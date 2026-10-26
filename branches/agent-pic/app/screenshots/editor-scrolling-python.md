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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.61 | 9.68 | 99% | 0.42 | 1.32 | 3.16 | 122.0 → 129.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.66 | 1.80 | 12.45 | 99% | 0.53 | 1.37 | 3.33 | 158.5 → 156.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 2.40 | 13.85 | 99% | 0.79 | 1.65 | 3.85 | 206.1 → 191.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.87 | 15.27 | 99% | 0.77 | 2.37 | 3.62 | 143.6 → 145.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.84 | 9.79 | 99% | 0.48 | 1.58 | 3.28 | 119.0 → 115.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.43 | 1.40 | 13.78 | 99% | 0.43 | 1.14 | 3.05 | 142.7 → 138.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 1.48 | 13.76 | 99% | 0.42 | 1.20 | 3.14 | 145.3 → 141.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 1.87 | 11.65 | 99% | 0.51 | 1.40 | 3.34 | 147.9 → 145.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 1.76 | 7.82 | 100% | 0.42 | 1.35 | 3.21 | 107.2 → 105.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 3.83 | 13.62 | 99% | 0.78 | 2.58 | 3.88 | 148.3 → 146.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.53 | 1.64 | 14.16 | 99% | 0.52 | 1.27 | 3.07 | 147.8 → 144.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.51 | 7.69 | 100% | 0.44 | 1.16 | 2.76 | 68.4 → 67.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 1.71 | 14.31 | 99% | 0.51 | 1.36 | 3.27 | 144.2 → 144.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.60 | 6.26 | 50.06 | 96% | 0.63 | 5.82 | 4.86 | 554.8 → 573.3 | 14 | 3.5 / 31 |

