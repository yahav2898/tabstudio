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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 2.26 | 14.27 | 99% | 0.39 | 1.97 | 2.82 | 93.5 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.57 | 16.08 | 99% | 1.35 | 2.05 | 3.44 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.65 | 16.08 | 99% | 1.56 | 2.11 | 3.54 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.50 | 15.35 | 99% | 0.86 | 2.06 | 3.47 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.41 | 15.75 | 99% | 1.17 | 1.95 | 3.38 | 91.6 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.54 | 2.35 | 16.75 | 99% | 0.62 | 1.94 | 2.89 | 115.4 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.57 | 2.47 | 16.68 | 99% | 0.53 | 2.08 | 3.13 | 117.4 → 113.4 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.39 | 15.82 | 99% | 2.20 | 2.34 | 3.47 | 120.2 → 116.1 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.58 | 2.77 | 14.21 | 100% | 1.40 | 2.26 | 3.32 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.59 | 2.53 | 11.42 | 100% | 0.58 | 2.09 | 3.31 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.56 | 2.38 | 16.01 | 99% | 0.83 | 2.03 | 2.87 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 2.05 | 10.46 | 100% | 0.45 | 1.79 | 2.59 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| ArrangingLayoutable NumberDrawing | 0.064 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 2.54 | 29.52 | 99% | 0.77 | 2.05 | 3.34 | 116.4 → 117.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.86 | 13.50 | 29.02 | 93% | 2.56 | 7.01 | 6.72 | 593.3 → 621.6 | 15 | 3.5 / 30 |

