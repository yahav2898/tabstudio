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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.86 | 16.40 | 99% | 0.42 | 2.56 | 4.33 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.23 | 16.89 | 99% | 1.40 | 2.68 | 5.07 | 131.7 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 5.18 | 83.56 | 99% | 2.56 | 2.85 | 5.56 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.00 | 16.47 | 99% | 0.70 | 2.59 | 4.83 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.82 | 19.14 | 99% | 0.68 | 2.41 | 4.93 | 92.3 → 90.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.92 | 18.59 | 99% | 0.66 | 2.51 | 4.88 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 3.45 | 84.06 | 99% | 1.56 | 2.79 | 5.23 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.12 | 15.60 | 99% | 0.92 | 2.65 | 5.19 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.88 | 12.85 | 100% | 0.50 | 2.52 | 4.68 | 81.7 → 79.6 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 3.07 | 16.31 | 99% | 0.67 | 2.64 | 4.96 | 121.6 → 118.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 5.26 | 17.84 | 99% | 1.83 | 4.58 | 5.03 | 121.8 → 118.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.57 | 11.88 | 100% | 0.44 | 2.29 | 4.02 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.09 | 16.82 | 99% | 0.64 | 2.62 | 4.85 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.22 | 10.19 | 43.01 | 93% | 2.41 | 8.93 | 9.35 | 617.5 → 643.3 | 15 | 3.5 / 31 |

