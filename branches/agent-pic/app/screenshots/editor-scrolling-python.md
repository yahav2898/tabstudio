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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.88 | 117.27 | 99% | 0.41 | 2.33 | 3.99 | 122.0 → 128.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 2.31 | 13.90 | 99% | 0.50 | 1.96 | 3.82 | 157.9 → 155.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.26 | 14.61 | 99% | 0.68 | 1.79 | 4.00 | 204.7 → 189.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.32 | 13.73 | 99% | 0.47 | 2.03 | 3.88 | 143.3 → 144.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.10 | 12.82 | 99% | 0.46 | 1.90 | 3.80 | 117.9 → 116.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 3.78 | 12.96 | 99% | 2.27 | 2.49 | 4.10 | 143.1 → 139.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.54 | 2.13 | 13.29 | 99% | 0.48 | 1.91 | 3.84 | 144.9 → 141.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 2.27 | 18.45 | 99% | 0.49 | 2.00 | 3.94 | 147.6 → 144.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.44 | 2.10 | 9.65 | 99% | 0.35 | 1.89 | 3.45 | 107.1 → 105.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 2.27 | 13.24 | 99% | 0.47 | 1.96 | 3.84 | 147.4 → 144.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 3.08 | 75.92 | 99% | 0.57 | 2.42 | 3.64 | 147.5 → 144.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.45 | 8.02 | 100% | 0.36 | 2.16 | 3.19 | 69.2 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.151 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| this editor | 368 | 0.53 | 2.20 | 14.00 | 99% | 0.48 | 1.89 | 3.76 | 143.5 → 144.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.59 | 4.47 | 21.29 | 96% | 0.55 | 3.90 | 4.02 | 547.5 → 567.2 | 13 | 3.5 / 31 |

