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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.95 | 14.70 | 99% | 0.51 | 2.58 | 4.52 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.58 | 17.10 | 100% | 1.32 | 2.74 | 5.05 | 131.3 → 129.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 4.85 | 18.77 | 99% | 1.93 | 2.99 | 5.94 | 178.0 → 163.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.32 | 18.59 | 99% | 0.78 | 2.79 | 5.01 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 3.00 | 20.79 | 99% | 0.72 | 2.53 | 5.21 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 3.66 | 14.36 | 99% | 1.25 | 2.77 | 4.98 | 116.4 → 112.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 4.07 | 85.81 | 99% | 1.49 | 2.81 | 5.27 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.17 | 16.66 | 99% | 1.15 | 2.58 | 4.88 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.84 | 13.60 | 99% | 0.48 | 2.49 | 4.78 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 3.29 | 26.47 | 99% | 1.30 | 2.71 | 5.03 | 121.2 → 118.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 3.66 | 17.64 | 99% | 1.47 | 2.74 | 4.72 | 121.4 → 117.8 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.63 | 16.18 | 100% | 0.46 | 2.31 | 4.13 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.287 |
| ArrangingLayoutable NumberDrawing | 0.074 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 3.23 | 18.09 | 99% | 0.78 | 2.75 | 4.90 | 117.2 → 118.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.43 | 9.95 | 90.08 | 93% | 1.69 | 7.38 | 9.48 | 613.8 → 632.9 | 15 | 3.5 / 31 |

