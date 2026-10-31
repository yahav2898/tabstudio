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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.65 | 2.91 | 100% | 0.32 | 1.41 | 3.32 | 93.4 → 100.6 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 2.04 | 51.02 | 100% | 0.66 | 1.59 | 4.20 | 129.7 → 130.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 1.96 | 4.06 | 100% | 1.01 | 1.45 | 4.33 | 176.8 → 162.7 | 0 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.00 | 53.27 | 100% | 0.57 | 1.64 | 4.44 | 115.1 → 116.1 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.87 | 2.88 | 100% | 0.51 | 1.50 | 4.19 | 91.5 → 89.2 | 0 | 1.2 / 29 |
| without the folding margin | 368 | 0.48 | 1.89 | 48.37 | 100% | 1.05 | 1.41 | 4.13 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.63 | 3.72 | 5.07 | 100% | 2.35 | 2.47 | 4.39 | 117.4 → 113.3 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.82 | 34.15 | 100% | 0.51 | 1.47 | 4.06 | 120.2 → 116.2 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.44 | 1.64 | 2.67 | 100% | 0.36 | 1.37 | 3.22 | 80.7 → 78.3 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.50 | 1.82 | 2.68 | 100% | 0.52 | 1.43 | 4.04 | 119.9 → 116.2 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.49 | 1.78 | 2.85 | 100% | 0.52 | 1.44 | 3.09 | 120.1 → 116.0 | 0 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.48 | 4.79 | 100% | 0.36 | 1.23 | 3.34 | 43.9 → 43.0 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.396 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 4.14 | 91.16 | 100% | 2.45 | 2.48 | 4.40 | 116.4 → 117.4 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.65 | 3.56 | 89.11 | 99% | 0.62 | 3.01 | 6.74 | 574.4 → 599.6 | 2 | 3.5 / 30 |

