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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.34 | 49.43 | 100% | 0.42 | 2.05 | 4.54 | 130.8 → 138.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.56 | 43.44 | 100% | 0.61 | 2.10 | 5.19 | 167.1 → 164.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.29 | 4.75 | 10.17 | 100% | 2.51 | 4.11 | 5.62 | 214.2 → 200.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.54 | 25.60 | 100% | 0.60 | 2.07 | 4.70 | 152.6 → 153.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.40 | 42.46 | 100% | 0.60 | 1.96 | 5.15 | 127.3 → 125.8 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.48 | 2.74 | 100% | 0.61 | 2.02 | 4.66 | 151.8 → 148.0 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.52 | 45.00 | 100% | 0.60 | 2.06 | 5.14 | 154.1 → 150.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 4.51 | 24.59 | 100% | 1.74 | 3.94 | 5.51 | 157.7 → 154.8 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.34 | 2.74 | 100% | 0.46 | 1.97 | 4.10 | 116.6 → 115.0 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.56 | 4.36 | 100% | 0.63 | 2.11 | 5.08 | 157.0 → 153.8 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.54 | 44.43 | 100% | 0.60 | 2.10 | 4.81 | 156.6 → 153.7 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.22 | 6.32 | 100% | 0.51 | 1.88 | 4.38 | 78.4 → 77.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.344 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable CachedLineNumberMargin | 0.040 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable TextView | 0.023 |
| RaisingRoutedEvent ScrollBar | 0.014 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.54 | 4.22 | 100% | 0.62 | 2.10 | 5.08 | 152.7 → 154.0 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 4.48 | 65.71 | 98% | 0.71 | 3.86 | 5.40 | 551.6 → 571.3 | 2 | 3.5 / 31 |

