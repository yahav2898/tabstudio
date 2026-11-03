# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.11 | 3.93 | 19.24 | 99% | 0.77 | 3.27 | 5.61 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.37 | 4.33 | 21.93 | 99% | 1.74 | 3.36 | 6.42 | 129.8 → 130.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.63 | 5.68 | 21.36 | 99% | 2.49 | 3.42 | 6.98 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.36 | 4.59 | 113.84 | 99% | 2.06 | 3.51 | 6.64 | 115.3 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.16 | 4.15 | 21.64 | 100% | 1.02 | 3.26 | 6.59 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 1.28 | 4.15 | 23.61 | 99% | 1.01 | 3.28 | 6.06 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.29 | 4.25 | 24.78 | 99% | 1.03 | 3.36 | 6.54 | 117.5 → 113.4 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.52 | 4.83 | 24.11 | 99% | 2.30 | 3.57 | 7.43 | 120.3 → 116.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 1.16 | 4.02 | 15.01 | 100% | 0.82 | 3.31 | 5.92 | 80.7 → 78.4 | 1 | 1.2 / 29 |
| without squiggles | 368 | 1.33 | 4.28 | 24.95 | 99% | 1.06 | 3.43 | 6.20 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.31 | 4.20 | 22.70 | 99% | 1.13 | 3.38 | 5.53 | 120.2 → 116.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.80 | 3.73 | 16.86 | 100% | 0.80 | 2.94 | 4.94 | 44.0 → 43.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.589 |
| AttachingStyle - | 0.058 |
| ArrangingLayoutable TextView | 0.040 |
| ArrangingLayoutable CachedLineNumberMargin | 0.036 |
| ArrangingLayoutable LineDrawing | 0.026 |
| FindingResource - | 0.021 |
| ArrangingLayoutable RepeatButton | 0.015 |
| ArrangingLayoutable ScrollingTextLayer | 0.011 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable NumberDrawing | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.009 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 3.97 | 47.74 | 99% | 0.86 | 3.17 | 6.04 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.03 | 10.66 | 33.10 | 93% | 1.24 | 7.94 | 10.54 | 605.7 → 631.8 | 15 | 3.5 / 30 |

