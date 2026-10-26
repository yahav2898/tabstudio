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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.55 | 10.19 | 100% | 0.37 | 1.24 | 2.92 | 121.2 → 128.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 1.72 | 12.66 | 99% | 0.54 | 1.33 | 3.33 | 158.1 → 155.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 1.87 | 13.89 | 99% | 0.67 | 1.36 | 3.17 | 205.1 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 1.76 | 10.54 | 99% | 0.50 | 1.37 | 3.15 | 143.2 → 144.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.34 | 10.62 | 99% | 0.56 | 1.36 | 3.12 | 117.6 → 116.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 3.24 | 12.74 | 99% | 0.71 | 2.53 | 3.41 | 143.7 → 138.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 1.69 | 11.98 | 99% | 0.51 | 1.29 | 3.09 | 144.6 → 141.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.69 | 12.93 | 99% | 0.51 | 1.30 | 3.17 | 147.9 → 144.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.44 | 7.56 | 100% | 0.38 | 1.17 | 2.71 | 106.6 → 104.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.77 | 11.50 | 99% | 0.55 | 1.35 | 3.17 | 147.4 → 144.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.82 | 16.94 | 99% | 0.54 | 2.35 | 3.06 | 147.4 → 144.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.34 | 8.08 | 100% | 0.47 | 1.26 | 2.80 | 68.2 → 67.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.176 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
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
| this editor | 368 | 0.55 | 1.65 | 13.28 | 99% | 0.51 | 1.27 | 2.99 | 143.6 → 144.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.40 | 3.53 | 21.29 | 96% | 0.53 | 2.90 | 3.46 | 544.4 → 564.0 | 13 | 3.5 / 31 |

