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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 2.45 | 13.29 | 100% | 0.37 | 2.18 | 3.57 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 2.63 | 13.97 | 99% | 1.10 | 2.21 | 3.81 | 131.7 → 129.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.74 | 2.88 | 16.48 | 99% | 1.36 | 2.26 | 4.21 | 178.6 → 164.0 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 4.45 | 18.20 | 99% | 1.66 | 2.50 | 4.44 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.62 | 13.49 | 99% | 0.59 | 2.18 | 4.03 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.78 | 16.15 | 99% | 0.95 | 2.30 | 4.20 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.81 | 14.80 | 99% | 0.73 | 2.33 | 4.29 | 118.9 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.73 | 14.43 | 99% | 0.69 | 2.33 | 3.83 | 121.8 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.72 | 12.18 | 99% | 1.38 | 2.35 | 4.15 | 81.6 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.79 | 15.91 | 99% | 0.77 | 2.32 | 3.90 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.67 | 14.91 | 99% | 0.75 | 2.26 | 3.34 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.34 | 10.83 | 100% | 0.45 | 2.05 | 3.30 | 43.7 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.284 |
| ArrangingLayoutable ScrollingTextLayer | 0.060 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.85 | 12.98 | 99% | 1.48 | 2.39 | 4.11 | 117.5 → 118.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.85 | 7.75 | 23.98 | 95% | 1.45 | 5.65 | 7.03 | 598.8 → 611.7 | 13 | 3.5 / 31 |

