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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.04 | 3.29 | 25.29 | 100% | 0.77 | 2.71 | 8.59 | 95.6 → 102.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 3.65 | 23.83 | 100% | 1.16 | 2.78 | 8.80 | 131.8 → 129.5 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.47 | 3.88 | 24.75 | 100% | 1.76 | 2.83 | 9.64 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.20 | 5.04 | 24.28 | 99% | 2.60 | 2.85 | 8.95 | 117.0 → 118.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 3.18 | 23.21 | 99% | 0.92 | 2.48 | 8.64 | 92.4 → 90.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.09 | 3.40 | 24.38 | 100% | 0.96 | 2.68 | 8.68 | 116.9 → 112.8 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.17 | 3.49 | 25.05 | 99% | 1.54 | 2.68 | 8.92 | 119.1 → 115.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.19 | 3.65 | 26.29 | 99% | 1.02 | 2.80 | 9.21 | 121.9 → 118.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.25 | 79.14 | 99% | 1.44 | 2.60 | 8.63 | 81.8 → 79.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 3.50 | 25.35 | 99% | 0.93 | 2.69 | 8.94 | 121.7 → 118.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.18 | 3.57 | 25.23 | 99% | 1.01 | 2.73 | 8.10 | 121.8 → 118.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 2.96 | 10.03 | 100% | 1.41 | 2.33 | 7.59 | 43.8 → 43.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.430 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable LineDrawing | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 3.46 | 24.83 | 99% | 0.97 | 2.68 | 8.71 | 117.7 → 118.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.14 | 9.39 | 39.28 | 92% | 1.98 | 8.35 | 16.27 | 639.1 → 647.0 | 11 | 3.5 / 31 |

