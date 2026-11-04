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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.14 | 3.83 | 20.03 | 99% | 0.72 | 3.15 | 5.89 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.38 | 5.05 | 82.84 | 99% | 2.02 | 3.24 | 6.21 | 129.8 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.52 | 4.89 | 23.11 | 99% | 2.02 | 3.26 | 6.38 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.06 | 3.94 | 15.39 | 99% | 0.92 | 3.09 | 5.78 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.38 | 19.43 | 99% | 1.54 | 2.77 | 5.42 | 91.5 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 3.75 | 80.48 | 99% | 1.55 | 2.86 | 5.34 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.18 | 4.00 | 18.99 | 99% | 1.00 | 3.16 | 6.14 | 117.4 → 113.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.27 | 4.11 | 19.93 | 99% | 1.59 | 3.20 | 6.05 | 120.3 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 1.10 | 3.82 | 14.32 | 99% | 1.65 | 3.10 | 6.00 | 80.8 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 1.21 | 3.97 | 42.50 | 99% | 1.15 | 3.18 | 6.06 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 1.19 | 4.04 | 20.98 | 99% | 1.53 | 3.17 | 5.51 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 3.55 | 21.39 | 100% | 1.71 | 2.81 | 5.53 | 44.0 → 43.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.410 |
| ArrangingLayoutable TextView | 0.027 |
| AttachingStyle - | 0.021 |
| FindingResource - | 0.018 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.33 | 4.10 | 24.79 | 100% | 1.11 | 3.37 | 6.50 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.26 | 13.61 | 45.33 | 91% | 2.20 | 8.84 | 10.90 | 609.0 → 631.9 | 14 | 3.5 / 30 |

