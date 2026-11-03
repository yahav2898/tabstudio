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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.95 | 16.85 | 99% | 0.44 | 2.65 | 4.37 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 3.54 | 18.08 | 99% | 0.95 | 2.80 | 5.08 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.48 | 18.42 | 99% | 1.60 | 2.70 | 5.34 | 176.9 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 4.24 | 16.18 | 99% | 1.66 | 2.94 | 5.23 | 115.2 → 116.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.94 | 18.05 | 99% | 0.64 | 2.50 | 5.02 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.66 | 3.14 | 27.21 | 99% | 0.74 | 2.61 | 4.57 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.70 | 3.22 | 16.40 | 99% | 0.77 | 2.69 | 5.08 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.90 | 3.68 | 16.87 | 99% | 1.62 | 2.86 | 5.61 | 120.3 → 116.2 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 3.19 | 9.12 | 100% | 0.71 | 2.69 | 4.94 | 80.7 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.70 | 3.14 | 18.16 | 99% | 0.78 | 2.60 | 4.56 | 120.0 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.71 | 3.08 | 17.00 | 99% | 0.73 | 2.63 | 4.10 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.63 | 12.93 | 100% | 0.44 | 2.32 | 4.13 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.369 |
| AttachingStyle - | 0.030 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.15 | 38.48 | 99% | 0.67 | 2.63 | 4.61 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.12 | 7.11 | 26.76 | 95% | 0.99 | 6.32 | 8.78 | 602.9 → 630.9 | 14 | 3.5 / 30 |

