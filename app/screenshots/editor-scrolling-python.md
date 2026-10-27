# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.42 | 42.93 | 100% | 0.42 | 2.09 | 4.56 | 131.3 → 138.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.66 | 43.22 | 100% | 0.65 | 2.13 | 4.66 | 167.7 → 165.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 2.87 | 4.22 | 100% | 0.87 | 2.19 | 5.32 | 214.4 → 199.7 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.82 | 46.20 | 100% | 0.62 | 2.25 | 5.07 | 152.8 → 154.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.88 | 86.97 | 99% | 0.72 | 2.30 | 5.26 | 128.2 → 126.2 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.44 | 3.10 | 100% | 0.62 | 2.01 | 4.76 | 152.2 → 148.5 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.61 | 43.29 | 100% | 0.64 | 2.13 | 5.19 | 154.5 → 151.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.52 | 45.79 | 100% | 0.63 | 2.08 | 4.94 | 157.4 → 154.1 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.02 | 5.75 | 100% | 0.55 | 2.51 | 5.25 | 117.2 → 115.8 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 2.84 | 10.55 | 100% | 0.68 | 2.22 | 5.17 | 157.3 → 154.1 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.49 | 40.04 | 100% | 0.61 | 2.05 | 4.48 | 157.0 → 154.0 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.19 | 2.70 | 100% | 0.46 | 1.83 | 4.23 | 78.4 → 77.8 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.220 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.81 | 5.04 | 100% | 0.65 | 2.24 | 5.22 | 153.0 → 154.6 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.34 | 5.12 | 64.98 | 98% | 0.70 | 4.61 | 5.50 | 557.6 → 575.8 | 2 | 3.5 / 31 |

