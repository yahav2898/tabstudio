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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.24 | 12.93 | 100% | 0.47 | 1.88 | 3.29 | 121.8 → 128.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.37 | 14.11 | 99% | 0.58 | 1.90 | 3.32 | 157.9 → 155.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 2.54 | 14.63 | 99% | 0.74 | 1.93 | 3.50 | 204.3 → 189.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 4.36 | 14.91 | 99% | 0.95 | 2.84 | 4.17 | 143.6 → 145.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 2.85 | 13.74 | 99% | 0.66 | 2.13 | 3.70 | 118.5 → 116.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.40 | 15.93 | 99% | 0.54 | 1.91 | 3.26 | 142.6 → 138.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.66 | 15.18 | 99% | 0.60 | 2.14 | 3.46 | 145.1 → 141.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.93 | 15.61 | 99% | 0.69 | 2.37 | 5.01 | 148.4 → 144.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.01 | 9.79 | 100% | 0.49 | 1.54 | 3.18 | 106.9 → 105.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 4.12 | 12.88 | 99% | 2.31 | 2.66 | 3.62 | 147.5 → 144.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.20 | 19.63 | 99% | 0.65 | 1.67 | 3.19 | 147.2 → 144.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 1.61 | 8.60 | 100% | 0.46 | 1.27 | 2.52 | 68.3 → 67.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.203 |
| AttachingStyle - | 0.085 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| this editor | 368 | 0.71 | 1.95 | 14.52 | 99% | 0.52 | 1.55 | 3.25 | 143.1 → 143.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.88 | 8.31 | 26.89 | 95% | 0.72 | 6.78 | 5.15 | 547.6 → 574.5 | 14 | 3.5 / 31 |

