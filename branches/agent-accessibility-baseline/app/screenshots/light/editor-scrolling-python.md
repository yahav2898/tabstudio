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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.78 | 16.34 | 100% | 0.40 | 2.53 | 4.11 | 95.2 → 102.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.07 | 18.76 | 99% | 1.24 | 2.60 | 4.98 | 131.2 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.16 | 5.12 | 19.50 | 99% | 2.94 | 3.00 | 5.86 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.99 | 18.42 | 99% | 0.64 | 2.57 | 4.70 | 116.5 → 117.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.74 | 16.36 | 99% | 0.64 | 2.39 | 4.75 | 91.8 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 2.91 | 16.79 | 99% | 0.71 | 2.50 | 4.77 | 116.3 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 4.93 | 89.04 | 99% | 2.41 | 2.82 | 5.16 | 118.5 → 115.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 3.02 | 17.12 | 99% | 0.75 | 2.55 | 4.76 | 121.4 → 117.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 2.84 | 15.46 | 99% | 0.49 | 2.52 | 4.67 | 81.2 → 79.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.95 | 17.70 | 99% | 0.70 | 2.56 | 4.72 | 121.1 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.26 | 16.51 | 100% | 1.35 | 2.72 | 4.47 | 121.2 → 117.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.89 | 17.45 | 100% | 0.50 | 2.46 | 4.00 | 43.3 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.98 | 18.49 | 99% | 0.68 | 2.54 | 4.46 | 117.1 → 117.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.26 | 12.76 | 31.71 | 92% | 1.72 | 9.87 | 9.00 | 608.9 → 636.4 | 15 | 3.5 / 31 |

