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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.48 | 16.89 | 99% | 0.44 | 2.05 | 2.99 | 94.0 → 101.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 2.64 | 16.81 | 99% | 0.93 | 2.12 | 3.36 | 130.6 → 131.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 3.11 | 20.84 | 99% | 1.58 | 2.26 | 3.93 | 178.1 → 163.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.71 | 15.94 | 99% | 0.64 | 2.15 | 3.41 | 115.9 → 116.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.55 | 15.71 | 99% | 0.72 | 2.03 | 3.43 | 91.8 → 89.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 2.78 | 16.74 | 100% | 1.20 | 2.18 | 3.36 | 116.2 → 111.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.87 | 4.64 | 28.76 | 99% | 2.68 | 2.53 | 4.51 | 118.2 → 114.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.80 | 15.61 | 99% | 1.14 | 2.23 | 3.66 | 121.0 → 117.0 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 2.61 | 14.33 | 99% | 0.53 | 2.16 | 3.10 | 80.8 → 78.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.89 | 2.96 | 34.35 | 99% | 1.47 | 2.33 | 3.92 | 120.7 → 116.9 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.82 | 3.31 | 17.30 | 99% | 1.24 | 2.42 | 3.60 | 120.9 → 116.8 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.33 | 13.27 | 100% | 0.53 | 1.93 | 2.83 | 43.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.308 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.02 | 30.09 | 99% | 1.14 | 2.37 | 3.86 | 117.1 → 118.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.22 | 6.35 | 28.86 | 96% | 0.85 | 4.71 | 6.72 | 577.9 → 608.3 | 14 | 3.5 / 30 |

