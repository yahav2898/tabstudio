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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.07 | 2.83 | 13.61 | 99% | 0.64 | 2.30 | 5.10 | 128.3 → 135.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.21 | 3.16 | 16.31 | 99% | 0.87 | 2.30 | 5.47 | 165.4 → 162.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.55 | 4.82 | 18.14 | 99% | 2.35 | 2.75 | 6.03 | 213.5 → 198.5 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.20 | 3.04 | 15.99 | 99% | 0.84 | 2.34 | 5.47 | 150.6 → 151.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.00 | 2.75 | 15.18 | 99% | 0.80 | 2.11 | 5.40 | 124.5 → 122.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.14 | 3.07 | 18.78 | 99% | 0.87 | 2.33 | 5.39 | 150.2 → 146.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.23 | 3.57 | 16.71 | 99% | 0.94 | 2.70 | 6.17 | 152.2 → 149.2 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.27 | 13.35 | 99% | 0.89 | 2.46 | 5.50 | 155.7 → 151.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.00 | 2.77 | 11.91 | 99% | 0.68 | 2.15 | 5.11 | 113.4 → 111.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.15 | 2.99 | 15.70 | 99% | 0.83 | 2.27 | 5.46 | 154.7 → 151.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.19 | 3.51 | 24.48 | 99% | 0.90 | 2.46 | 5.03 | 154.4 → 151.4 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.78 | 2.71 | 10.39 | 100% | 0.69 | 1.89 | 4.44 | 74.8 → 74.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.308 |
| AttachingStyle - | 0.066 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.17 | 3.02 | 13.37 | 99% | 0.83 | 2.32 | 5.26 | 150.8 → 151.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.01 | 7.70 | 33.28 | 95% | 1.01 | 6.70 | 5.98 | 557.8 → 586.0 | 13 | 3.5 / 31 |

