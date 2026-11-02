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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.19 | 16.23 | 99% | 0.53 | 2.78 | 4.76 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.41 | 17.94 | 99% | 0.97 | 2.80 | 4.88 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.87 | 16.96 | 99% | 1.73 | 2.79 | 5.48 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 4.26 | 20.22 | 99% | 1.73 | 2.90 | 5.98 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 3.20 | 15.49 | 99% | 0.80 | 2.60 | 5.06 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.70 | 3.26 | 16.14 | 100% | 0.73 | 2.65 | 5.02 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.75 | 3.36 | 18.33 | 99% | 0.92 | 2.76 | 5.14 | 117.4 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.52 | 18.06 | 99% | 1.63 | 2.81 | 5.32 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.71 | 3.60 | 19.75 | 99% | 1.47 | 2.80 | 5.52 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 3.24 | 16.43 | 99% | 0.77 | 2.73 | 4.84 | 120.0 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.75 | 3.27 | 18.54 | 99% | 0.73 | 2.75 | 3.98 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.67 | 4.00 | 100% | 0.48 | 2.35 | 4.14 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| FindingResource - | 0.078 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
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
| this editor | 368 | 0.77 | 3.25 | 39.62 | 99% | 0.77 | 2.75 | 4.72 | 116.3 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.16 | 6.93 | 29.45 | 96% | 1.55 | 5.82 | 9.19 | 604.9 → 630.6 | 14 | 3.5 / 30 |

