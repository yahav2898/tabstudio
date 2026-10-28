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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 2.98 | 23.57 | 99% | 0.61 | 2.45 | 5.66 | 95.6 → 102.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 4.01 | 22.39 | 100% | 1.69 | 2.54 | 6.32 | 131.7 → 129.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.28 | 3.49 | 22.90 | 99% | 1.67 | 2.62 | 6.69 | 178.7 → 164.0 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 3.22 | 21.94 | 99% | 0.88 | 2.57 | 6.23 | 117.0 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 3.34 | 20.83 | 100% | 1.53 | 2.47 | 6.57 | 92.3 → 90.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.07 | 3.27 | 24.57 | 99% | 1.41 | 2.56 | 6.80 | 116.9 → 112.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.10 | 3.34 | 23.10 | 99% | 0.97 | 2.59 | 6.91 | 119.0 → 115.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.42 | 23.91 | 99% | 1.21 | 2.64 | 6.62 | 121.9 → 118.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.98 | 3.22 | 14.46 | 100% | 0.74 | 2.63 | 6.50 | 81.8 → 79.7 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 3.40 | 24.48 | 99% | 0.98 | 2.64 | 6.54 | 121.7 → 118.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 3.46 | 22.52 | 100% | 0.95 | 2.66 | 6.05 | 121.8 → 118.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.94 | 76.21 | 100% | 1.38 | 2.28 | 5.65 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.313 |
| AttachingStyle - | 0.098 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 3.30 | 23.85 | 99% | 0.91 | 2.57 | 5.93 | 117.6 → 118.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.98 | 9.82 | 35.37 | 90% | 1.99 | 8.79 | 12.13 | 625.5 → 646.7 | 11 | 3.5 / 31 |

