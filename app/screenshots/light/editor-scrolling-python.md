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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 2.24 | 14.14 | 99% | 0.48 | 1.95 | 3.97 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 2.22 | 15.66 | 99% | 0.92 | 1.92 | 3.09 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.48 | 2.28 | 15.92 | 99% | 0.98 | 1.88 | 3.10 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 2.18 | 11.51 | 99% | 0.50 | 1.91 | 3.10 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.32 | 2.05 | 13.63 | 99% | 0.43 | 1.80 | 2.88 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.35 | 2.16 | 12.86 | 99% | 0.45 | 1.90 | 2.60 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.48 | 2.70 | 20.75 | 99% | 1.32 | 2.27 | 3.39 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.53 | 3.26 | 19.14 | 99% | 1.48 | 2.35 | 3.24 | 119.9 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.32 | 2.07 | 17.50 | 100% | 0.69 | 1.82 | 2.90 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.38 | 2.16 | 13.74 | 99% | 0.42 | 1.90 | 2.92 | 119.6 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.36 | 2.13 | 13.12 | 99% | 0.42 | 1.87 | 2.55 | 119.7 → 115.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.87 | 13.68 | 100% | 0.31 | 1.72 | 2.30 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| ArrangingLayoutable ScrollingTextLayer | 0.045 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.44 | 2.50 | 34.61 | 99% | 1.00 | 2.02 | 2.85 | 116.0 → 117.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.92 | 8.71 | 26.00 | 95% | 2.37 | 6.43 | 5.84 | 581.9 → 593.1 | 13 | 3.5 / 30 |

