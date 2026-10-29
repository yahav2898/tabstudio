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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.68 | 15.18 | 99% | 0.51 | 2.25 | 3.39 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 2.75 | 15.23 | 100% | 1.09 | 2.25 | 3.22 | 131.7 → 129.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 5.02 | 17.89 | 99% | 2.61 | 3.51 | 4.07 | 178.6 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.76 | 20.09 | 99% | 0.69 | 2.25 | 3.58 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.47 | 15.51 | 99% | 1.25 | 2.02 | 3.38 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.73 | 22.35 | 99% | 1.02 | 2.24 | 3.56 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.78 | 17.14 | 99% | 0.66 | 2.26 | 3.56 | 118.9 → 115.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.91 | 14.97 | 99% | 1.38 | 2.27 | 3.44 | 121.8 → 118.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.98 | 14.57 | 99% | 1.27 | 2.47 | 3.48 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.91 | 15.32 | 99% | 0.71 | 2.34 | 3.54 | 121.5 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.87 | 23.89 | 99% | 0.78 | 2.30 | 3.06 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.33 | 4.10 | 100% | 0.47 | 1.98 | 2.67 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.257 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.98 | 14.91 | 99% | 0.68 | 2.40 | 3.58 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.55 | 11.61 | 29.44 | 93% | 1.60 | 10.22 | 6.74 | 607.1 → 613.1 | 14 | 3.5 / 31 |

