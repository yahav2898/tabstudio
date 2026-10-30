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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.15 | 16.49 | 99% | 0.50 | 2.76 | 4.45 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.77 | 18.12 | 99% | 1.44 | 2.80 | 5.14 | 131.3 → 129.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.75 | 19.27 | 98% | 2.83 | 2.96 | 5.80 | 177.9 → 163.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 3.38 | 17.35 | 99% | 1.04 | 2.76 | 5.21 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.21 | 11.86 | 99% | 0.74 | 2.59 | 5.16 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.33 | 15.92 | 99% | 1.44 | 2.78 | 5.12 | 116.4 → 112.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 3.51 | 16.59 | 99% | 1.15 | 2.82 | 5.11 | 118.6 → 115.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 3.43 | 15.43 | 99% | 0.79 | 2.87 | 4.85 | 121.4 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 3.45 | 12.91 | 99% | 0.65 | 2.89 | 4.67 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.93 | 5.14 | 26.27 | 99% | 1.76 | 3.11 | 5.70 | 121.2 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 3.51 | 17.25 | 99% | 0.82 | 2.88 | 4.86 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.93 | 16.24 | 100% | 0.59 | 2.49 | 4.21 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.394 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| this editor | 368 | 0.75 | 3.22 | 15.19 | 99% | 0.75 | 2.69 | 4.82 | 117.2 → 118.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.33 | 10.87 | 23.81 | 93% | 1.69 | 8.41 | 9.27 | 617.9 → 635.1 | 14 | 3.5 / 31 |

