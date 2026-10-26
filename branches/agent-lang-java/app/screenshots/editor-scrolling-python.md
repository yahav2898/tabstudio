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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.78 | 9.27 | 99% | 0.41 | 1.46 | 2.89 | 121.7 → 128.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 1.60 | 10.67 | 99% | 0.48 | 1.27 | 2.89 | 158.2 → 156.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 1.75 | 11.68 | 99% | 0.62 | 1.31 | 3.02 | 205.5 → 190.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.71 | 13.45 | 99% | 2.38 | 2.64 | 3.33 | 143.8 → 145.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 1.46 | 8.52 | 100% | 0.47 | 1.15 | 2.91 | 117.5 → 116.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 1.61 | 12.79 | 99% | 0.47 | 1.28 | 2.89 | 143.3 → 138.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 1.61 | 11.67 | 99% | 0.47 | 1.24 | 2.99 | 144.9 → 142.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 1.76 | 11.62 | 99% | 0.53 | 1.33 | 3.12 | 148.3 → 144.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 1.83 | 9.10 | 99% | 0.47 | 1.41 | 3.01 | 107.4 → 105.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.93 | 11.44 | 99% | 0.66 | 2.30 | 3.42 | 148.4 → 145.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 1.64 | 17.65 | 99% | 0.51 | 1.31 | 2.67 | 147.8 → 144.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 1.20 | 6.29 | 100% | 0.32 | 0.97 | 2.27 | 68.0 → 67.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.156 |
| ArrangingLayoutable ScrollingTextLayer | 0.039 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 1.88 | 13.73 | 99% | 0.56 | 1.41 | 3.16 | 143.7 → 145.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.22 | 6.36 | 25.65 | 96% | 0.89 | 5.17 | 3.65 | 551.6 → 572.2 | 13 | 3.5 / 31 |

