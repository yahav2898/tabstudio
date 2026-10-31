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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.94 | 16.26 | 99% | 0.47 | 2.62 | 4.19 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.16 | 17.70 | 99% | 1.00 | 2.62 | 4.46 | 129.2 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.58 | 19.00 | 99% | 1.54 | 2.67 | 5.17 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 4.42 | 17.79 | 99% | 1.97 | 2.80 | 5.50 | 114.8 → 115.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.84 | 13.29 | 99% | 0.64 | 2.43 | 4.87 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 2.93 | 23.13 | 99% | 0.74 | 2.49 | 4.46 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 2.95 | 17.47 | 100% | 0.72 | 2.54 | 4.95 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.26 | 15.47 | 99% | 0.82 | 2.66 | 5.09 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.68 | 3.39 | 14.32 | 99% | 1.36 | 2.79 | 4.83 | 80.3 → 78.0 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.64 | 3.07 | 15.88 | 99% | 0.71 | 2.60 | 4.42 | 119.5 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.65 | 2.99 | 17.10 | 99% | 0.67 | 2.57 | 3.88 | 119.6 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.68 | 11.60 | 100% | 0.45 | 2.37 | 4.07 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| ArrangingLayoutable LineDrawing | 0.085 |
| AttachingStyle - | 0.067 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.79 | 3.91 | 101.95 | 99% | 1.61 | 2.96 | 5.28 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.72 | 6.62 | 32.50 | 95% | 0.90 | 5.85 | 8.64 | 594.9 → 625.2 | 14 | 3.5 / 30 |

