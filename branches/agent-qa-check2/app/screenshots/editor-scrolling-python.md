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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.12 | 4.30 | 100% | 0.46 | 2.76 | 4.44 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 4.35 | 20.01 | 99% | 1.56 | 3.02 | 5.37 | 131.7 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.41 | 4.19 | 20.38 | 99% | 1.91 | 3.18 | 5.94 | 178.7 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.07 | 3.88 | 15.17 | 99% | 0.94 | 3.08 | 5.93 | 117.0 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.64 | 14.23 | 99% | 1.75 | 2.92 | 5.53 | 92.4 → 90.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 3.64 | 17.15 | 99% | 1.55 | 2.98 | 5.49 | 116.9 → 112.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 3.65 | 15.03 | 99% | 0.80 | 3.00 | 5.32 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 3.60 | 17.82 | 99% | 0.81 | 2.91 | 5.30 | 121.9 → 118.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 3.11 | 17.89 | 99% | 1.34 | 2.68 | 4.73 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 3.55 | 14.55 | 99% | 1.24 | 2.86 | 5.30 | 121.7 → 118.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 3.06 | 20.22 | 99% | 1.15 | 2.63 | 4.61 | 121.8 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.60 | 14.31 | 100% | 0.42 | 2.29 | 3.97 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.214 |
| ArrangingLayoutable ScrollingTextLayer | 0.051 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 3.63 | 17.74 | 99% | 1.68 | 2.91 | 5.51 | 117.6 → 118.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.05 | 8.48 | 26.51 | 95% | 0.84 | 6.69 | 9.07 | 619.6 → 639.8 | 14 | 3.5 / 31 |

