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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 1.50 | 9.85 | 99% | 0.33 | 1.17 | 3.10 | 121.2 → 128.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.53 | 1.59 | 11.68 | 99% | 0.44 | 1.24 | 2.94 | 157.8 → 155.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 1.92 | 12.68 | 99% | 0.72 | 1.42 | 3.35 | 205.2 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.14 | 11.65 | 99% | 0.51 | 1.51 | 3.18 | 143.5 → 144.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.76 | 7.67 | 100% | 0.52 | 2.30 | 3.08 | 117.9 → 116.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.42 | 1.46 | 12.32 | 99% | 0.45 | 1.15 | 2.88 | 142.2 → 138.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 1.71 | 11.40 | 99% | 0.48 | 1.29 | 3.05 | 144.6 → 141.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.69 | 12.26 | 99% | 0.50 | 1.34 | 3.18 | 147.8 → 144.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 1.42 | 8.80 | 99% | 0.32 | 1.17 | 2.75 | 106.3 → 104.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.43 | 1.45 | 10.72 | 99% | 0.39 | 1.20 | 2.84 | 147.1 → 144.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.57 | 3.33 | 64.11 | 99% | 2.39 | 2.50 | 3.23 | 147.9 → 144.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.09 | 5.66 | 100% | 0.29 | 0.93 | 2.34 | 67.4 → 67.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.256 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 1.79 | 13.99 | 99% | 0.50 | 1.36 | 3.34 | 143.7 → 144.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.22 | 3.08 | 19.56 | 96% | 0.47 | 2.60 | 3.14 | 544.9 → 560.1 | 12 | 3.5 / 31 |

