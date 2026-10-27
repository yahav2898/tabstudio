# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.19 | 12.28 | 99% | 0.42 | 1.91 | 4.45 | 130.8 → 138.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.93 | 15.49 | 99% | 0.65 | 2.48 | 4.76 | 167.0 → 164.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.27 | 4.95 | 99.47 | 99% | 2.68 | 3.02 | 5.58 | 215.1 → 199.9 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.69 | 15.37 | 99% | 0.61 | 2.33 | 4.78 | 152.5 → 153.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.72 | 12.88 | 99% | 0.59 | 2.34 | 4.76 | 127.3 → 125.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.78 | 15.76 | 99% | 0.59 | 2.45 | 4.66 | 151.9 → 148.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 4.20 | 83.54 | 99% | 0.69 | 2.67 | 4.85 | 154.3 → 151.8 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.85 | 14.35 | 99% | 0.62 | 2.45 | 4.97 | 157.0 → 153.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.66 | 12.17 | 99% | 0.47 | 2.36 | 4.74 | 116.8 → 115.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.41 | 13.80 | 99% | 0.57 | 2.11 | 4.58 | 156.7 → 153.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.14 | 14.40 | 99% | 0.64 | 2.51 | 4.26 | 156.7 → 153.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.54 | 80.49 | 99% | 0.50 | 2.21 | 4.07 | 78.5 → 77.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.65 | 14.82 | 99% | 0.60 | 2.26 | 4.58 | 152.7 → 153.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.29 | 5.61 | 28.11 | 96% | 0.69 | 4.70 | 5.23 | 557.0 → 576.1 | 14 | 3.5 / 31 |

