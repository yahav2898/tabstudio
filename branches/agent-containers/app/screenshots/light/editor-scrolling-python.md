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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.60 | 1.85 | 100% | 0.31 | 1.38 | 3.27 | 93.2 → 100.4 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.91 | 46.66 | 100% | 0.56 | 1.48 | 4.02 | 129.3 → 129.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.19 | 48.58 | 100% | 0.86 | 1.64 | 4.65 | 176.1 → 162.1 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.97 | 3.09 | 100% | 0.53 | 1.61 | 3.77 | 114.8 → 115.8 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.41 | 1.63 | 45.53 | 100% | 0.49 | 1.32 | 3.96 | 91.1 → 88.9 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.47 | 1.81 | 3.39 | 100% | 0.55 | 1.46 | 3.67 | 115.0 → 110.4 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.55 | 3.68 | 48.22 | 100% | 2.33 | 2.35 | 4.23 | 117.0 → 113.0 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.52 | 1.87 | 5.62 | 100% | 0.55 | 1.46 | 3.66 | 119.8 → 115.8 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.44 | 1.67 | 42.35 | 100% | 0.37 | 1.38 | 3.79 | 80.3 → 78.0 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.49 | 1.77 | 2.31 | 100% | 0.50 | 1.43 | 3.39 | 119.5 → 115.8 | 0 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.53 | 2.00 | 53.87 | 100% | 0.54 | 1.61 | 3.56 | 119.6 → 115.7 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.65 | 2.28 | 100% | 0.37 | 1.38 | 3.65 | 43.6 → 42.7 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.169 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.96 | 4.64 | 100% | 2.29 | 2.32 | 4.28 | 116.0 → 117.1 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.71 | 3.60 | 75.26 | 99% | 0.64 | 3.09 | 6.83 | 568.1 → 592.8 | 3 | 3.5 / 30 |

