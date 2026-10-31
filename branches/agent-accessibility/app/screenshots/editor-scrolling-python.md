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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.73 | 16.95 | 99% | 0.51 | 2.28 | 3.28 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.05 | 18.88 | 99% | 1.40 | 2.41 | 3.97 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 3.06 | 19.65 | 99% | 1.54 | 2.38 | 3.84 | 176.8 → 162.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 2.78 | 16.45 | 99% | 0.83 | 2.25 | 3.89 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.66 | 14.79 | 99% | 0.68 | 2.12 | 3.79 | 91.5 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.76 | 4.68 | 20.10 | 99% | 2.41 | 2.50 | 3.83 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.71 | 3.05 | 17.33 | 99% | 1.30 | 2.29 | 3.71 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.78 | 16.77 | 99% | 0.62 | 2.26 | 3.70 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.57 | 2.54 | 10.42 | 100% | 0.45 | 2.14 | 3.40 | 80.7 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.68 | 2.72 | 16.35 | 99% | 1.03 | 2.22 | 3.74 | 119.9 → 116.1 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.71 | 2.91 | 19.74 | 99% | 0.71 | 2.33 | 3.21 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.44 | 13.51 | 100% | 0.73 | 2.04 | 2.92 | 43.9 → 43.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.382 |
| ArrangingLayoutable Border | 0.069 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| MeasuringLayoutable CodeEditorView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 2.77 | 39.12 | 99% | 0.66 | 2.28 | 3.57 | 116.3 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.83 | 5.45 | 25.80 | 96% | 0.97 | 4.47 | 6.13 | 576.5 → 602.3 | 13 | 3.5 / 30 |

