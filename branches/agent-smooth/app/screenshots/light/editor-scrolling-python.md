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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.91 | 4.26 | 100% | 0.44 | 2.60 | 4.41 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.96 | 75.66 | 99% | 1.78 | 2.86 | 5.21 | 129.3 → 129.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.64 | 16.92 | 99% | 1.61 | 2.76 | 5.50 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.31 | 16.27 | 99% | 0.74 | 2.77 | 5.14 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 3.26 | 14.47 | 99% | 1.48 | 2.65 | 5.23 | 91.1 → 88.9 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.75 | 3.49 | 13.19 | 100% | 1.41 | 2.79 | 5.35 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.75 | 3.41 | 16.67 | 99% | 0.86 | 2.78 | 4.94 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.29 | 15.79 | 99% | 0.75 | 2.74 | 5.18 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.68 | 3.34 | 14.52 | 100% | 1.31 | 2.72 | 4.98 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.27 | 19.83 | 99% | 0.80 | 2.74 | 4.85 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 3.42 | 15.79 | 99% | 0.75 | 2.78 | 4.70 | 119.6 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.92 | 12.53 | 100% | 0.56 | 2.50 | 4.16 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.37 | 32.93 | 99% | 0.71 | 2.75 | 4.81 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.43 | 13.03 | 88.41 | 94% | 1.82 | 9.17 | 9.29 | 600.8 → 624.2 | 15 | 3.5 / 30 |

