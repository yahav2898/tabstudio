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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 2.85 | 12.03 | 100% | 0.43 | 2.56 | 4.26 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.07 | 15.42 | 99% | 0.92 | 2.60 | 4.45 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 3.36 | 17.74 | 99% | 1.40 | 2.64 | 5.15 | 177.9 → 163.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 4.15 | 13.92 | 99% | 1.66 | 2.84 | 5.01 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.85 | 14.76 | 99% | 1.04 | 2.46 | 5.07 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.94 | 16.41 | 99% | 0.71 | 2.52 | 4.72 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 3.03 | 17.35 | 99% | 0.68 | 2.61 | 4.82 | 118.5 → 115.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 4.53 | 8.63 | 100% | 1.68 | 2.82 | 5.17 | 121.5 → 118.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.84 | 12.18 | 99% | 0.44 | 2.50 | 4.60 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 3.06 | 15.79 | 99% | 0.70 | 2.59 | 4.72 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 3.01 | 15.42 | 99% | 1.16 | 2.60 | 4.17 | 121.3 → 117.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.85 | 8.50 | 100% | 1.08 | 2.45 | 4.19 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.207 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.99 | 15.61 | 99% | 0.67 | 2.57 | 4.54 | 117.2 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.27 | 10.18 | 111.33 | 93% | 1.75 | 7.05 | 8.91 | 609.9 → 635.3 | 15 | 3.5 / 31 |

