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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 2.39 | 14.57 | 99% | 0.34 | 2.16 | 3.51 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.56 | 15.24 | 99% | 1.29 | 2.18 | 3.64 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.83 | 15.97 | 99% | 1.47 | 2.21 | 4.21 | 176.9 → 162.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 2.50 | 15.84 | 99% | 0.54 | 2.15 | 3.56 | 115.2 → 116.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.46 | 15.87 | 99% | 0.54 | 2.07 | 3.96 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.63 | 3.82 | 18.40 | 99% | 1.69 | 2.35 | 3.96 | 115.5 → 110.8 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.59 | 2.72 | 14.94 | 99% | 1.32 | 2.29 | 4.13 | 117.5 → 113.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.54 | 2.56 | 14.64 | 99% | 0.60 | 2.18 | 3.62 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.44 | 2.40 | 10.95 | 99% | 0.38 | 2.10 | 3.20 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.54 | 2.55 | 14.45 | 99% | 0.54 | 2.20 | 3.48 | 120.0 → 116.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.50 | 2.51 | 16.84 | 99% | 0.56 | 2.15 | 2.98 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 2.35 | 11.42 | 100% | 1.30 | 2.03 | 3.29 | 44.0 → 43.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.429 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
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
| this editor | 368 | 0.53 | 2.55 | 36.78 | 99% | 0.59 | 2.18 | 3.74 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.78 | 6.17 | 24.93 | 95% | 0.64 | 5.32 | 6.80 | 578.3 → 607.4 | 14 | 3.5 / 30 |

