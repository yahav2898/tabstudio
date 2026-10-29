# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.15 | 4.35 | 13.66 | 99% | 0.69 | 3.65 | 7.22 | 135.3 → 143.5 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 4.35 | 96.82 | 98% | 0.97 | 3.50 | 7.26 | 172.2 → 169.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.58 | 4.66 | 22.21 | 99% | 1.12 | 3.56 | 7.46 | 219.1 → 204.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.31 | 4.45 | 17.28 | 99% | 0.85 | 3.56 | 7.21 | 156.7 → 159.2 | 5 | 1.1 / 4 |
| without line numbers | 368 | 1.03 | 3.99 | 14.96 | 99% | 0.77 | 3.31 | 6.90 | 132.4 → 130.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.18 | 3.93 | 19.46 | 99% | 0.82 | 3.36 | 7.06 | 156.6 → 152.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.25 | 4.36 | 17.16 | 100% | 0.86 | 3.47 | 7.25 | 158.6 → 156.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 4.09 | 15.14 | 99% | 0.76 | 3.38 | 7.17 | 162.1 → 158.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 3.72 | 11.49 | 99% | 0.59 | 3.18 | 6.56 | 120.4 → 118.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 4.14 | 16.65 | 99% | 0.78 | 3.41 | 7.05 | 160.8 → 158.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.33 | 4.55 | 18.29 | 99% | 0.88 | 3.56 | 6.56 | 161.8 → 158.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.79 | 3.55 | 10.18 | 99% | 0.58 | 3.03 | 5.68 | 82.5 → 81.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.328 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.016 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.33 | 6.23 | 18.89 | 98% | 1.09 | 3.94 | 7.65 | 157.9 → 159.7 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.24 | 6.89 | 37.02 | 96% | 0.94 | 6.15 | 7.34 | 557.5 → 577.9 | 13 | 3.5 / 31 |

