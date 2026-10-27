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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.80 | 13.29 | 99% | 0.41 | 2.52 | 4.31 | 131.2 → 138.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.95 | 16.09 | 99% | 0.63 | 2.55 | 4.75 | 167.7 → 165.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 3.31 | 17.29 | 99% | 0.81 | 2.71 | 5.28 | 214.2 → 199.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 5.04 | 18.56 | 99% | 0.78 | 4.10 | 5.65 | 153.6 → 155.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.85 | 16.69 | 99% | 0.60 | 2.50 | 5.15 | 127.9 → 126.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 3.00 | 18.64 | 99% | 0.62 | 2.57 | 4.82 | 152.3 → 148.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.89 | 17.53 | 99% | 0.61 | 2.53 | 4.83 | 154.6 → 151.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.20 | 16.76 | 99% | 0.63 | 2.65 | 4.72 | 157.2 → 154.7 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.37 | 83.72 | 99% | 0.60 | 2.87 | 4.94 | 117.7 → 115.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 3.03 | 18.26 | 99% | 0.60 | 2.62 | 4.86 | 156.9 → 154.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.80 | 14.84 | 99% | 0.60 | 2.40 | 4.54 | 157.2 → 154.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.34 | 10.51 | 100% | 0.45 | 2.24 | 4.11 | 79.1 → 78.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.276 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 4.87 | 18.77 | 99% | 1.11 | 3.25 | 5.17 | 153.9 → 155.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.13 | 5.71 | 24.19 | 96% | 0.72 | 4.91 | 5.21 | 557.4 → 575.0 | 13 | 3.5 / 31 |

