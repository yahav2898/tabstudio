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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.97 | 12.65 | 100% | 0.46 | 2.60 | 4.36 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 3.18 | 16.49 | 99% | 0.88 | 2.64 | 4.65 | 129.3 → 129.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 3.57 | 16.23 | 99% | 1.46 | 2.65 | 5.16 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 4.15 | 14.18 | 99% | 1.54 | 2.75 | 5.03 | 114.8 → 115.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 3.07 | 15.10 | 100% | 0.85 | 2.57 | 5.08 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.75 | 3.25 | 25.12 | 99% | 0.77 | 2.72 | 4.75 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.84 | 3.61 | 16.71 | 99% | 0.85 | 2.79 | 5.15 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.88 | 4.70 | 13.65 | 99% | 1.88 | 2.82 | 5.22 | 119.8 → 115.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.62 | 2.96 | 13.22 | 100% | 0.53 | 2.54 | 4.13 | 80.2 → 77.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.26 | 15.15 | 99% | 0.76 | 2.70 | 5.26 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.74 | 3.09 | 15.53 | 99% | 0.75 | 2.64 | 4.01 | 119.6 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.70 | 13.09 | 100% | 0.48 | 2.37 | 4.09 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.452 |
| AttachingStyle - | 0.041 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| EvaluatingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 3.28 | 34.95 | 99% | 0.77 | 2.68 | 4.52 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.46 | 9.01 | 30.32 | 94% | 1.70 | 7.35 | 9.16 | 597.8 → 627.3 | 15 | 3.5 / 30 |

