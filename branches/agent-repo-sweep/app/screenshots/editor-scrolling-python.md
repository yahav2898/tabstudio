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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.47 | 15.31 | 99% | 0.48 | 2.05 | 2.83 | 95.6 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.66 | 15.15 | 100% | 0.72 | 2.13 | 3.21 | 131.7 → 129.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 2.96 | 15.77 | 99% | 1.44 | 2.22 | 3.54 | 178.7 → 163.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 4.21 | 16.07 | 99% | 1.59 | 2.55 | 3.77 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.54 | 13.28 | 99% | 0.59 | 2.02 | 3.31 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.62 | 13.86 | 99% | 0.70 | 2.08 | 3.18 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.54 | 14.11 | 99% | 0.86 | 2.09 | 3.34 | 118.9 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.62 | 13.94 | 99% | 0.65 | 2.12 | 3.31 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.55 | 11.48 | 99% | 0.49 | 2.06 | 3.08 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.65 | 14.91 | 99% | 1.63 | 2.43 | 4.40 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.61 | 15.41 | 99% | 0.63 | 2.11 | 2.67 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.18 | 16.98 | 100% | 0.72 | 1.83 | 2.66 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| ArrangingLayoutable NumberDrawing | 0.049 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
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
| this editor | 368 | 0.74 | 2.72 | 15.47 | 99% | 0.61 | 2.20 | 3.40 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.68 | 10.53 | 28.68 | 94% | 1.68 | 9.91 | 6.36 | 596.8 → 629.1 | 15 | 3.5 / 31 |

