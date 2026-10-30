# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 3.13 | 14.22 | 99% | 0.51 | 2.71 | 5.31 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.98 | 4.40 | 82.57 | 99% | 1.67 | 2.90 | 5.44 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 3.62 | 17.19 | 99% | 1.61 | 2.78 | 5.47 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.46 | 16.95 | 99% | 0.88 | 2.83 | 5.63 | 117.0 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 3.12 | 18.81 | 99% | 0.79 | 2.57 | 5.13 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 4.66 | 85.00 | 99% | 2.56 | 2.92 | 5.61 | 116.8 → 112.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 3.76 | 18.37 | 99% | 0.86 | 2.92 | 5.34 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 3.32 | 20.32 | 99% | 0.80 | 2.75 | 5.01 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 3.09 | 14.03 | 99% | 1.22 | 2.65 | 4.98 | 81.7 → 79.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 4.25 | 21.80 | 99% | 1.71 | 2.99 | 5.39 | 121.7 → 118.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 3.57 | 16.86 | 99% | 0.82 | 2.96 | 4.86 | 121.8 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.76 | 10.95 | 100% | 0.53 | 2.42 | 4.25 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| AttachingStyle - | 0.063 |
| ArrangingLayoutable TextView | 0.017 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 4.90 | 15.11 | 99% | 1.78 | 3.05 | 5.44 | 117.6 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.41 | 7.60 | 26.96 | 95% | 0.93 | 6.65 | 9.17 | 621.2 → 642.4 | 14 | 3.5 / 31 |

