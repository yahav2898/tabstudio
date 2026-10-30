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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 3.17 | 16.54 | 99% | 0.53 | 2.82 | 4.98 | 95.6 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 4.59 | 19.42 | 99% | 1.69 | 2.99 | 5.59 | 131.8 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 3.98 | 17.64 | 99% | 1.66 | 2.90 | 5.78 | 178.7 → 164.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 3.41 | 16.53 | 99% | 0.78 | 2.85 | 5.35 | 117.0 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.24 | 22.05 | 99% | 0.74 | 2.67 | 5.39 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.94 | 3.99 | 16.97 | 99% | 1.52 | 3.09 | 5.54 | 116.9 → 112.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 3.68 | 17.61 | 99% | 0.88 | 2.90 | 5.45 | 119.0 → 115.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 3.47 | 18.18 | 99% | 0.77 | 2.86 | 5.36 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 3.40 | 14.62 | 99% | 1.36 | 2.80 | 5.04 | 81.7 → 79.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.94 | 4.28 | 14.25 | 99% | 1.47 | 3.05 | 6.51 | 121.7 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 3.42 | 16.42 | 99% | 0.76 | 2.84 | 4.91 | 121.8 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 3.01 | 12.24 | 100% | 0.59 | 2.55 | 4.47 | 43.7 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.267 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 5.01 | 16.84 | 99% | 1.83 | 3.12 | 5.83 | 117.7 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.86 | 7.71 | 34.75 | 95% | 1.08 | 6.72 | 10.70 | 621.4 → 641.2 | 14 | 3.5 / 31 |

