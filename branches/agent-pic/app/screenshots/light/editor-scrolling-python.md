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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 1.59 | 9.89 | 99% | 0.32 | 1.36 | 3.34 | 120.9 → 128.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.19 | 18.08 | 99% | 0.72 | 2.42 | 4.08 | 157.3 → 155.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 2.97 | 13.50 | 99% | 0.86 | 1.52 | 4.05 | 203.8 → 189.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 1.79 | 12.40 | 99% | 0.48 | 1.44 | 3.76 | 142.5 → 143.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.61 | 12.57 | 99% | 0.45 | 1.30 | 3.79 | 117.3 → 115.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 1.83 | 15.36 | 99% | 0.49 | 1.43 | 3.83 | 142.0 → 138.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 2.65 | 14.94 | 99% | 0.54 | 2.08 | 3.76 | 144.6 → 141.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.90 | 15.27 | 99% | 0.62 | 2.35 | 4.31 | 147.9 → 144.6 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.59 | 11.29 | 99% | 0.36 | 1.34 | 3.62 | 107.0 → 104.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 1.75 | 12.57 | 99% | 0.48 | 1.39 | 3.70 | 146.6 → 143.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.50 | 1.76 | 13.20 | 99% | 0.47 | 1.42 | 3.26 | 146.6 → 143.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.38 | 1.76 | 100% | 0.33 | 1.17 | 3.14 | 68.2 → 67.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| ArrangingLayoutable LineDrawing | 0.049 |
| AttachingStyle - | 0.021 |
| FindingResource - | 0.019 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 3.12 | 11.91 | 99% | 0.70 | 2.34 | 3.98 | 143.7 → 144.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 3.70 | 21.41 | 96% | 0.56 | 3.25 | 4.09 | 542.0 → 560.9 | 12 | 3.5 / 31 |

