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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 2.17 | 9.62 | 100% | 0.32 | 1.93 | 2.64 | 93.4 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.54 | 2.41 | 14.33 | 99% | 0.88 | 2.00 | 2.93 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 2.57 | 15.83 | 99% | 1.26 | 2.03 | 3.38 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.49 | 2.30 | 14.93 | 99% | 0.47 | 1.97 | 2.92 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 2.22 | 16.86 | 99% | 0.48 | 1.89 | 3.14 | 91.6 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.45 | 2.27 | 15.65 | 99% | 0.52 | 1.93 | 2.85 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.60 | 2.81 | 15.76 | 99% | 1.47 | 2.17 | 3.32 | 117.4 → 113.4 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.66 | 3.61 | 90.72 | 99% | 1.44 | 2.45 | 3.67 | 120.3 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.42 | 2.22 | 13.68 | 99% | 0.66 | 1.95 | 3.10 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.50 | 2.46 | 14.72 | 99% | 0.86 | 2.04 | 3.41 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.48 | 2.28 | 14.65 | 99% | 0.61 | 1.97 | 2.65 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 2.02 | 11.02 | 100% | 0.37 | 1.79 | 2.60 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| AttachingStyle - | 0.061 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.57 | 2.80 | 36.81 | 99% | 1.40 | 2.15 | 3.26 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.75 | 7.32 | 37.90 | 95% | 1.38 | 5.73 | 5.98 | 584.6 → 600.7 | 14 | 3.5 / 30 |

