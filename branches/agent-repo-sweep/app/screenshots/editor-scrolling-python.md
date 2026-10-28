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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.23 | 17.54 | 100% | 0.50 | 2.79 | 4.66 | 95.5 → 102.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.01 | 4.63 | 20.16 | 99% | 1.67 | 2.99 | 5.58 | 131.7 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 3.72 | 19.30 | 99% | 1.56 | 2.87 | 5.54 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.40 | 15.96 | 99% | 0.87 | 2.78 | 5.12 | 117.0 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.37 | 18.73 | 99% | 1.46 | 2.70 | 5.21 | 92.3 → 90.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.41 | 15.58 | 100% | 1.23 | 2.81 | 5.45 | 116.9 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.35 | 20.40 | 100% | 1.14 | 2.79 | 5.24 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.33 | 16.31 | 99% | 1.13 | 2.77 | 5.15 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 4.14 | 81.52 | 99% | 1.45 | 2.73 | 5.09 | 81.7 → 79.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 3.42 | 16.91 | 99% | 0.96 | 2.81 | 5.51 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.26 | 17.12 | 99% | 0.76 | 2.74 | 4.52 | 121.7 → 118.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.78 | 15.97 | 100% | 0.53 | 2.39 | 4.27 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.363 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.014 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.49 | 14.45 | 99% | 1.41 | 2.88 | 5.27 | 117.6 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.26 | 10.54 | 23.06 | 95% | 1.38 | 6.14 | 9.10 | 621.2 → 642.3 | 15 | 3.5 / 31 |

