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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 3.44 | 15.67 | 99% | 0.56 | 2.97 | 6.99 | 95.4 → 102.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 4.06 | 96.44 | 99% | 2.11 | 3.09 | 7.33 | 131.3 → 129.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 3.80 | 16.79 | 99% | 1.82 | 2.97 | 7.47 | 178.0 → 163.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.86 | 3.50 | 13.47 | 99% | 0.78 | 2.96 | 6.99 | 116.6 → 118.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.61 | 14.58 | 99% | 1.78 | 2.85 | 7.13 | 91.9 → 90.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 3.44 | 17.66 | 99% | 0.84 | 2.93 | 7.22 | 116.5 → 112.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.44 | 13.92 | 99% | 0.74 | 2.89 | 7.29 | 118.6 → 115.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.76 | 14.76 | 99% | 0.96 | 2.98 | 7.19 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.45 | 15.49 | 99% | 1.65 | 2.92 | 6.95 | 81.4 → 79.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.52 | 16.68 | 99% | 0.79 | 2.94 | 6.97 | 121.2 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.44 | 16.15 | 99% | 0.73 | 2.87 | 6.76 | 121.4 → 117.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 3.31 | 12.42 | 100% | 1.68 | 2.79 | 6.30 | 43.4 → 42.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.247 |
| ArrangingLayoutable ScrollingTextLayer | 0.110 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 3.55 | 15.60 | 99% | 0.86 | 2.93 | 7.11 | 117.3 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.65 | 12.55 | 96.93 | 88% | 2.44 | 10.14 | 13.76 | 620.4 → 639.6 | 15 | 3.5 / 31 |

