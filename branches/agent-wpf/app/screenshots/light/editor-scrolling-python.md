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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.43 | 1.69 | 8.79 | 100% | 0.36 | 1.23 | 2.96 | 121.0 → 128.7 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 3.21 | 13.40 | 99% | 0.70 | 2.29 | 3.53 | 158.5 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.51 | 1.70 | 14.60 | 99% | 0.62 | 1.17 | 3.19 | 204.9 → 189.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.44 | 1.51 | 10.85 | 99% | 0.47 | 1.21 | 2.92 | 142.8 → 144.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 1.24 | 9.11 | 99% | 0.43 | 1.06 | 2.78 | 117.0 → 115.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 1.32 | 12.06 | 99% | 0.42 | 1.02 | 2.93 | 142.1 → 138.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.42 | 1.80 | 9.82 | 99% | 0.40 | 1.47 | 2.85 | 144.5 → 141.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.56 | 18.58 | 99% | 2.17 | 2.54 | 3.51 | 148.4 → 144.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.63 | 16.25 | 99% | 0.28 | 1.45 | 2.77 | 106.3 → 104.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.47 | 1.88 | 10.35 | 99% | 0.41 | 1.56 | 2.93 | 147.2 → 144.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.43 | 1.63 | 11.56 | 99% | 0.40 | 1.42 | 2.73 | 147.2 → 144.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.26 | 1.48 | 8.34 | 100% | 0.34 | 1.31 | 2.40 | 67.9 → 67.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.216 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 2.69 | 16.55 | 99% | 0.58 | 1.63 | 2.86 | 143.6 → 144.8 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 3.76 | 18.38 | 97% | 0.44 | 3.14 | 3.24 | 546.4 → 563.8 | 12 | 3.5 / 31 |

