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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 3.25 | 13.76 | 100% | 0.49 | 2.75 | 4.41 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.45 | 17.02 | 99% | 0.92 | 2.82 | 5.14 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 3.97 | 18.36 | 99% | 1.78 | 2.91 | 5.47 | 176.8 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.69 | 119.84 | 99% | 1.34 | 2.92 | 5.29 | 115.2 → 116.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 3.08 | 34.85 | 99% | 0.68 | 2.61 | 5.01 | 91.5 → 89.2 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.66 | 3.10 | 17.32 | 99% | 0.71 | 2.60 | 4.57 | 115.4 → 110.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.75 | 3.45 | 19.88 | 99% | 0.99 | 2.74 | 5.21 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.93 | 4.32 | 20.18 | 99% | 2.53 | 2.96 | 5.65 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.59 | 2.96 | 9.49 | 100% | 0.51 | 2.56 | 4.73 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.71 | 3.24 | 17.75 | 99% | 0.77 | 2.70 | 4.53 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.69 | 3.11 | 15.88 | 99% | 0.75 | 2.65 | 3.82 | 120.0 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.69 | 4.11 | 100% | 0.48 | 2.35 | 4.03 | 43.9 → 43.0 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.369 |
| ArrangingLayoutable CachedLineNumberMargin | 0.042 |
| AttachingStyle - | 0.041 |
| ArrangingLayoutable TextView | 0.017 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 3.14 | 38.91 | 99% | 0.68 | 2.67 | 4.56 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.20 | 8.80 | 44.30 | 94% | 1.71 | 7.14 | 9.02 | 602.9 → 630.7 | 15 | 3.5 / 30 |

