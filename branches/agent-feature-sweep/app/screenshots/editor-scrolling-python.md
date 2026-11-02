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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 2.04 | 10.19 | 100% | 0.26 | 1.86 | 2.43 | 93.4 → 100.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 2.52 | 17.65 | 99% | 0.74 | 2.04 | 3.11 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 2.87 | 16.81 | 99% | 1.47 | 2.14 | 3.55 | 176.8 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.53 | 16.52 | 99% | 0.60 | 2.11 | 3.25 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.34 | 14.82 | 100% | 0.71 | 2.00 | 3.21 | 91.5 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.56 | 2.44 | 17.77 | 99% | 0.61 | 2.05 | 3.27 | 115.4 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.63 | 3.63 | 21.00 | 99% | 1.50 | 2.33 | 3.57 | 117.4 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.76 | 15.53 | 99% | 1.28 | 2.13 | 3.10 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.34 | 2.08 | 13.38 | 100% | 0.34 | 1.83 | 2.58 | 80.7 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.45 | 2.25 | 15.60 | 99% | 0.50 | 1.91 | 3.02 | 120.0 → 116.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.43 | 2.22 | 20.25 | 99% | 0.50 | 1.91 | 2.74 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 2.03 | 9.46 | 100% | 0.43 | 1.76 | 2.65 | 43.9 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.008 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.05 | 37.47 | 99% | 1.43 | 2.36 | 3.36 | 116.4 → 117.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.86 | 9.68 | 98.66 | 94% | 1.43 | 7.96 | 6.32 | 583.5 → 604.6 | 14 | 3.5 / 30 |

