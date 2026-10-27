# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.09 | 12.51 | 99% | 0.46 | 1.70 | 3.81 | 129.0 → 136.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.35 | 14.52 | 99% | 0.63 | 1.82 | 4.33 | 165.1 → 162.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.16 | 15.66 | 99% | 0.86 | 2.18 | 4.50 | 211.6 → 197.1 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 3.02 | 87.51 | 99% | 0.97 | 2.45 | 4.51 | 150.8 → 151.7 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 1.96 | 12.61 | 99% | 0.56 | 1.53 | 4.03 | 124.9 → 122.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.61 | 1.88 | 14.37 | 99% | 0.52 | 1.55 | 3.88 | 149.0 → 145.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.12 | 14.26 | 99% | 0.55 | 1.66 | 4.08 | 152.0 → 148.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.05 | 13.08 | 99% | 0.54 | 1.62 | 3.99 | 154.4 → 151.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 2.85 | 10.17 | 99% | 0.53 | 2.31 | 4.05 | 114.1 → 112.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.85 | 13.97 | 99% | 0.59 | 2.37 | 4.17 | 154.9 → 151.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.06 | 12.58 | 99% | 0.54 | 1.63 | 3.54 | 153.8 → 150.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.61 | 8.90 | 100% | 0.38 | 1.34 | 3.30 | 75.4 → 74.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.193 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.28 | 15.25 | 99% | 0.60 | 1.71 | 3.95 | 150.2 → 151.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.97 | 7.07 | 29.51 | 95% | 2.48 | 6.36 | 5.87 | 559.0 → 579.5 | 13 | 3.5 / 31 |

