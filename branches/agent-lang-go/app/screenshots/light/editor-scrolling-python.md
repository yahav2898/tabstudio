# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 2.22 | 12.90 | 99% | 0.51 | 1.78 | 4.55 | 122.9 → 130.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 3.17 | 16.58 | 99% | 0.83 | 2.30 | 5.14 | 159.4 → 158.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 2.68 | 16.14 | 99% | 0.97 | 1.96 | 5.33 | 206.5 → 191.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.43 | 12.10 | 99% | 0.69 | 1.85 | 4.94 | 144.6 → 145.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.18 | 15.78 | 99% | 0.69 | 1.68 | 4.86 | 119.3 → 117.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 4.26 | 17.18 | 99% | 0.79 | 3.75 | 5.31 | 144.8 → 140.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.31 | 14.54 | 99% | 0.67 | 1.81 | 4.89 | 146.5 → 142.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 2.31 | 13.99 | 99% | 0.71 | 1.83 | 4.95 | 149.0 → 145.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 2.69 | 10.15 | 99% | 0.56 | 1.96 | 4.66 | 108.2 → 106.8 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 2.85 | 12.98 | 99% | 0.73 | 2.23 | 5.16 | 149.2 → 146.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 2.40 | 14.82 | 99% | 0.69 | 1.88 | 4.50 | 149.0 → 145.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 1.87 | 12.32 | 100% | 0.50 | 1.54 | 4.06 | 69.2 → 68.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.365 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable Track | 0.018 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.013 |
| MeasuringLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 2.94 | 14.41 | 99% | 0.72 | 2.29 | 5.27 | 145.5 → 145.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 4.94 | 24.80 | 96% | 0.80 | 4.06 | 5.63 | 551.7 → 565.0 | 12 | 3.5 / 31 |

