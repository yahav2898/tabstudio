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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.06 | 24.56 | 100% | 0.61 | 2.50 | 5.78 | 93.3 → 100.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.31 | 24.58 | 99% | 1.09 | 2.51 | 6.89 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.67 | 5.62 | 92.23 | 98% | 2.92 | 2.75 | 7.65 | 176.2 → 162.2 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 3.26 | 22.74 | 99% | 0.87 | 2.55 | 6.00 | 114.9 → 115.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 3.10 | 21.49 | 100% | 0.89 | 2.35 | 6.80 | 91.2 → 89.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 1.04 | 3.55 | 24.74 | 99% | 1.60 | 2.58 | 7.12 | 115.1 → 110.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.12 | 4.41 | 18.14 | 100% | 1.85 | 2.64 | 6.97 | 117.1 → 113.0 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.10 | 3.26 | 26.49 | 99% | 0.91 | 2.57 | 6.86 | 119.9 → 115.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.93 | 3.12 | 21.54 | 100% | 0.65 | 2.50 | 6.53 | 80.3 → 78.0 | 1 | 1.2 / 29 |
| without squiggles | 368 | 1.13 | 3.60 | 23.09 | 99% | 1.75 | 2.58 | 7.08 | 119.6 → 115.9 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.11 | 3.57 | 22.49 | 99% | 1.58 | 2.61 | 5.44 | 119.8 → 115.7 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.67 | 2.84 | 21.49 | 100% | 0.67 | 2.26 | 5.85 | 43.6 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.328 |
| AttachingStyle - | 0.112 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.18 | 4.39 | 47.61 | 100% | 1.76 | 2.64 | 7.08 | 116.1 → 117.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.69 | 7.00 | 36.80 | 96% | 1.08 | 5.95 | 11.81 | 601.7 → 625.2 | 9 | 3.5 / 30 |

