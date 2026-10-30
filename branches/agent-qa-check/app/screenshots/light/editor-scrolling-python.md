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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.91 | 13.11 | 99% | 0.40 | 2.59 | 4.37 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 4.23 | 17.03 | 99% | 1.33 | 2.82 | 5.04 | 131.3 → 129.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 4.10 | 16.61 | 99% | 1.59 | 2.72 | 5.26 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 3.07 | 15.09 | 99% | 0.65 | 2.62 | 4.82 | 116.6 → 118.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.87 | 15.55 | 99% | 0.64 | 2.44 | 4.84 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 3.99 | 19.54 | 99% | 1.46 | 2.79 | 5.03 | 116.4 → 112.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 3.04 | 15.03 | 99% | 0.69 | 2.61 | 4.92 | 118.6 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.04 | 16.60 | 99% | 0.75 | 2.64 | 4.76 | 121.5 → 117.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 2.86 | 12.50 | 100% | 0.46 | 2.53 | 4.66 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 4.96 | 17.90 | 99% | 1.55 | 2.92 | 5.07 | 121.2 → 118.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 3.10 | 14.39 | 99% | 0.66 | 2.66 | 4.60 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.59 | 3.87 | 100% | 0.43 | 2.29 | 4.01 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.212 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.27 | 14.43 | 99% | 0.65 | 2.67 | 5.15 | 117.2 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.20 | 12.97 | 84.41 | 93% | 1.54 | 8.43 | 8.68 | 617.9 → 634.2 | 14 | 3.5 / 31 |

