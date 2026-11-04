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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.31 | 1.87 | 13.17 | 99% | 0.24 | 1.71 | 2.25 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.39 | 2.01 | 13.12 | 100% | 0.74 | 1.73 | 2.76 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.46 | 2.22 | 14.10 | 99% | 0.84 | 1.80 | 2.89 | 176.1 → 162.0 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.35 | 1.98 | 12.64 | 99% | 0.38 | 1.76 | 2.65 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.30 | 1.94 | 12.52 | 99% | 0.68 | 1.68 | 2.84 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.33 | 1.97 | 15.00 | 99% | 0.40 | 1.73 | 2.73 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.35 | 2.06 | 17.24 | 99% | 0.36 | 1.80 | 2.58 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.47 | 2.73 | 16.13 | 99% | 1.33 | 2.25 | 2.99 | 119.8 → 115.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.39 | 2.54 | 9.65 | 99% | 0.68 | 2.04 | 2.65 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.36 | 2.03 | 12.28 | 99% | 0.55 | 1.75 | 2.65 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.32 | 1.96 | 14.43 | 99% | 0.39 | 1.72 | 2.17 | 119.6 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.17 | 1.72 | 12.66 | 100% | 0.23 | 1.57 | 2.12 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.143 |
| AttachingStyle - | 0.090 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 1.95 | 18.90 | 99% | 0.41 | 1.72 | 2.64 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.13 | 8.80 | 102.22 | 95% | 2.45 | 6.37 | 6.19 | 579.3 → 603.4 | 15 | 3.5 / 30 |

