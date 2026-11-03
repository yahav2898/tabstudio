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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.16 | 14.99 | 100% | 0.50 | 2.73 | 4.40 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.52 | 17.36 | 99% | 1.40 | 2.85 | 4.89 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.14 | 3.83 | 19.21 | 99% | 1.83 | 3.01 | 5.77 | 176.2 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.00 | 4.58 | 100.42 | 99% | 2.27 | 3.01 | 5.73 | 114.9 → 115.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 3.60 | 18.69 | 99% | 1.00 | 2.82 | 5.64 | 91.2 → 89.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.94 | 3.69 | 15.20 | 99% | 0.89 | 2.98 | 5.42 | 115.1 → 110.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.07 | 3.78 | 21.01 | 99% | 0.97 | 3.11 | 5.85 | 117.1 → 113.1 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.07 | 3.90 | 20.27 | 99% | 1.62 | 3.08 | 5.28 | 119.9 → 115.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 4.35 | 11.63 | 99% | 1.58 | 2.86 | 5.47 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.81 | 3.48 | 16.27 | 99% | 0.79 | 2.90 | 5.34 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.79 | 3.44 | 23.71 | 99% | 1.08 | 2.87 | 4.77 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.94 | 11.33 | 100% | 0.51 | 2.55 | 4.23 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| AttachingStyle - | 0.175 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.28 | 33.58 | 99% | 0.74 | 2.72 | 4.53 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.43 | 7.25 | 36.54 | 95% | 0.98 | 6.19 | 9.57 | 599.6 → 625.1 | 14 | 3.5 / 30 |

