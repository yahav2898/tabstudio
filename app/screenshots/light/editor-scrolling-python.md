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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.79 | 3.42 | 21.04 | 99% | 0.65 | 2.88 | 5.98 | 93.3 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 3.49 | 22.52 | 99% | 1.70 | 2.90 | 5.99 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 3.67 | 24.60 | 99% | 1.91 | 2.90 | 6.31 | 176.2 → 162.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.24 | 20.21 | 99% | 0.82 | 2.74 | 5.52 | 114.9 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.24 | 19.26 | 99% | 1.26 | 2.62 | 5.48 | 91.1 → 89.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 1.17 | 4.15 | 135.05 | 99% | 2.25 | 3.27 | 6.37 | 115.1 → 110.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.95 | 3.72 | 27.11 | 99% | 0.86 | 2.98 | 6.64 | 117.1 → 113.1 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.47 | 19.49 | 99% | 0.89 | 2.76 | 5.44 | 119.9 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.70 | 3.39 | 13.33 | 100% | 0.72 | 2.75 | 5.23 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.00 | 3.64 | 20.11 | 100% | 0.87 | 2.92 | 6.07 | 119.6 → 115.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.81 | 3.82 | 19.45 | 99% | 1.69 | 2.88 | 5.12 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 3.03 | 18.24 | 100% | 1.74 | 2.53 | 5.07 | 43.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.254 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 3.40 | 41.37 | 99% | 0.77 | 2.73 | 5.13 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.33 | 9.73 | 36.35 | 93% | 1.09 | 8.43 | 11.30 | 599.2 → 627.0 | 15 | 3.5 / 30 |

