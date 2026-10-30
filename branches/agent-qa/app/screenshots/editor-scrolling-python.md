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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.63 | 14.78 | 99% | 0.52 | 2.15 | 3.27 | 95.6 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.12 | 16.91 | 99% | 1.45 | 2.43 | 3.50 | 131.7 → 129.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 4.43 | 18.39 | 98% | 2.72 | 2.50 | 3.95 | 178.7 → 164.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 2.83 | 14.91 | 99% | 0.63 | 2.30 | 3.60 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 2.59 | 16.09 | 99% | 0.57 | 2.07 | 3.35 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 2.75 | 16.39 | 99% | 0.83 | 2.23 | 3.68 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.74 | 17.14 | 99% | 0.62 | 2.15 | 3.56 | 118.9 → 115.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.94 | 4.01 | 17.87 | 99% | 2.06 | 2.48 | 4.05 | 121.8 → 118.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.63 | 12.30 | 99% | 0.75 | 2.11 | 3.43 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 2.84 | 16.74 | 99% | 0.91 | 2.24 | 3.79 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.75 | 19.11 | 99% | 0.66 | 2.22 | 3.15 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.48 | 11.18 | 100% | 0.57 | 2.07 | 2.92 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.257 |
| ArrangingLayoutable ScrollingTextLayer | 0.058 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
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
| this editor | 368 | 0.91 | 3.90 | 14.08 | 99% | 1.51 | 2.50 | 4.13 | 117.6 → 118.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.92 | 5.62 | 23.81 | 96% | 0.82 | 4.39 | 6.52 | 591.9 → 612.8 | 13 | 3.5 / 31 |

