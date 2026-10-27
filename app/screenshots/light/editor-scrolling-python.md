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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 3.62 | 15.26 | 99% | 0.56 | 3.13 | 5.33 | 133.5 → 141.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 3.83 | 18.00 | 99% | 0.77 | 3.12 | 5.49 | 170.2 → 167.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.51 | 5.86 | 20.68 | 99% | 2.59 | 4.83 | 5.85 | 217.5 → 202.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 3.84 | 16.40 | 99% | 0.78 | 3.15 | 5.52 | 155.6 → 157.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.88 | 3.68 | 16.54 | 99% | 0.73 | 3.06 | 5.55 | 129.5 → 128.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 3.93 | 17.57 | 99% | 0.85 | 3.17 | 5.68 | 155.3 → 151.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.28 | 4.55 | 18.84 | 99% | 0.89 | 3.69 | 8.07 | 158.1 → 154.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.16 | 4.00 | 15.63 | 99% | 0.81 | 3.37 | 5.59 | 160.2 → 157.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.86 | 3.63 | 24.71 | 99% | 0.57 | 3.11 | 5.11 | 118.7 → 116.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.20 | 4.42 | 85.00 | 99% | 0.88 | 3.37 | 5.82 | 160.6 → 157.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.05 | 3.82 | 13.35 | 99% | 0.73 | 3.19 | 4.88 | 159.7 → 156.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.67 | 3.34 | 12.16 | 100% | 0.58 | 2.88 | 4.46 | 78.8 → 78.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.298 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.19 | 4.18 | 18.49 | 99% | 0.83 | 3.31 | 5.74 | 156.2 → 157.5 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.61 | 7.40 | 30.14 | 95% | 0.93 | 6.22 | 6.04 | 557.1 → 581.0 | 12 | 3.5 / 31 |

