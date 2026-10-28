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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.91 | 15.25 | 99% | 0.45 | 2.61 | 4.24 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 4.39 | 16.36 | 99% | 1.73 | 2.82 | 5.17 | 131.3 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.48 | 16.46 | 99% | 1.41 | 2.72 | 5.22 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.16 | 15.84 | 99% | 0.68 | 2.70 | 4.97 | 116.6 → 117.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.00 | 13.95 | 99% | 0.68 | 2.50 | 4.88 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 4.74 | 16.28 | 99% | 2.41 | 2.77 | 5.21 | 116.4 → 112.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 3.18 | 16.82 | 99% | 0.69 | 2.66 | 4.87 | 118.5 → 115.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.11 | 18.89 | 99% | 0.73 | 2.66 | 4.90 | 121.5 → 117.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.53 | 2.90 | 11.51 | 99% | 0.48 | 2.55 | 4.62 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 3.71 | 16.83 | 99% | 1.69 | 2.84 | 5.14 | 121.2 → 118.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 3.04 | 15.05 | 99% | 0.69 | 2.61 | 4.15 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.67 | 7.37 | 100% | 0.56 | 2.34 | 4.08 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.309 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.09 | 16.12 | 99% | 0.63 | 2.67 | 4.74 | 117.2 → 117.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.24 | 14.95 | 28.89 | 93% | 1.61 | 10.69 | 9.01 | 617.1 → 636.2 | 15 | 3.5 / 31 |

