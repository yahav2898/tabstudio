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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 3.80 | 20.07 | 99% | 0.70 | 3.14 | 5.54 | 95.4 → 102.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.33 | 4.42 | 20.45 | 100% | 1.79 | 3.29 | 6.09 | 131.3 → 129.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.48 | 5.13 | 107.35 | 99% | 2.29 | 3.29 | 6.77 | 178.0 → 163.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.18 | 3.95 | 19.31 | 99% | 0.96 | 3.18 | 6.01 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.97 | 3.63 | 18.35 | 99% | 0.97 | 2.97 | 5.92 | 92.0 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.23 | 4.27 | 28.58 | 99% | 1.96 | 3.22 | 6.40 | 116.5 → 112.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.21 | 3.90 | 18.85 | 99% | 1.14 | 3.11 | 6.02 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.13 | 3.85 | 19.78 | 99% | 1.02 | 3.03 | 5.84 | 121.6 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.95 | 3.66 | 16.32 | 99% | 0.76 | 2.92 | 5.67 | 81.4 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.24 | 4.66 | 25.24 | 99% | 2.26 | 3.11 | 6.20 | 121.3 → 118.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 3.90 | 22.43 | 99% | 0.88 | 3.12 | 5.10 | 121.3 → 117.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.67 | 3.32 | 19.70 | 100% | 0.69 | 2.73 | 4.74 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.335 |
| AttachingStyle - | 0.063 |
| ArrangingLayoutable TextView | 0.026 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.21 | 3.93 | 20.26 | 100% | 1.03 | 3.21 | 5.97 | 117.3 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.97 | 9.62 | 28.57 | 94% | 1.23 | 8.22 | 10.51 | 612.8 → 635.1 | 14 | 3.5 / 31 |

