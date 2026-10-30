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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 2.08 | 12.98 | 99% | 0.27 | 1.94 | 2.46 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 2.22 | 13.90 | 99% | 0.54 | 1.91 | 2.89 | 131.7 → 129.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.57 | 2.80 | 30.44 | 99% | 0.97 | 2.04 | 3.52 | 178.7 → 164.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 3.79 | 14.60 | 99% | 2.01 | 2.32 | 3.25 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 2.09 | 13.04 | 99% | 0.45 | 1.81 | 2.86 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 2.15 | 14.68 | 99% | 0.42 | 1.87 | 2.90 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 2.19 | 13.48 | 99% | 0.42 | 1.90 | 2.94 | 119.0 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.41 | 2.15 | 11.32 | 100% | 0.46 | 1.90 | 2.82 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.32 | 2.04 | 10.27 | 100% | 0.31 | 1.82 | 2.64 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.43 | 2.67 | 12.99 | 99% | 0.86 | 2.00 | 2.78 | 121.6 → 118.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.51 | 3.07 | 80.04 | 99% | 2.31 | 2.30 | 2.70 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 1.85 | 7.68 | 100% | 0.25 | 1.70 | 2.18 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.174 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 2.09 | 12.55 | 99% | 0.40 | 1.85 | 2.68 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 4.79 | 23.77 | 96% | 1.13 | 3.91 | 5.31 | 585.6 → 602.0 | 14 | 3.5 / 31 |

