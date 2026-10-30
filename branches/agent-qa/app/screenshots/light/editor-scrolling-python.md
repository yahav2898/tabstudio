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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 2.83 | 15.78 | 99% | 0.59 | 2.32 | 3.73 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 3.45 | 17.18 | 99% | 1.63 | 2.46 | 4.14 | 131.2 → 129.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 5.13 | 88.28 | 98% | 1.87 | 2.58 | 4.14 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.91 | 16.59 | 99% | 0.73 | 2.32 | 4.05 | 116.5 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 2.97 | 15.31 | 99% | 1.49 | 2.29 | 4.00 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.88 | 3.00 | 18.48 | 99% | 0.81 | 2.39 | 3.74 | 116.4 → 112.3 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.91 | 3.02 | 17.58 | 99% | 0.74 | 2.42 | 3.92 | 118.5 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.06 | 4.81 | 16.27 | 99% | 1.77 | 2.66 | 3.87 | 121.4 → 118.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 2.87 | 13.73 | 99% | 0.60 | 2.36 | 3.68 | 81.2 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.89 | 15.69 | 99% | 0.74 | 2.33 | 3.98 | 121.2 → 117.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.74 | 15.04 | 99% | 0.66 | 2.25 | 3.06 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.49 | 13.29 | 100% | 0.54 | 2.03 | 2.95 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.262 |
| AttachingStyle - | 0.073 |
| ArrangingLayoutable LineDrawing | 0.060 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.94 | 3.20 | 17.39 | 99% | 1.55 | 2.50 | 3.77 | 117.2 → 117.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.56 | 7.28 | 33.52 | 95% | 1.34 | 5.83 | 7.17 | 603.2 → 616.9 | 14 | 3.5 / 31 |

