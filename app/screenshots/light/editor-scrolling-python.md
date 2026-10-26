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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.88 | 2.40 | 12.10 | 99% | 0.55 | 1.89 | 4.09 | 128.4 → 136.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.74 | 14.77 | 99% | 0.75 | 2.01 | 4.39 | 164.5 → 162.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.31 | 4.99 | 18.99 | 99% | 2.89 | 3.90 | 4.91 | 212.0 → 197.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.15 | 14.01 | 99% | 0.61 | 1.72 | 3.94 | 149.8 → 150.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 2.31 | 11.13 | 99% | 0.67 | 1.84 | 4.14 | 124.8 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 2.36 | 21.84 | 99% | 0.63 | 1.83 | 4.21 | 149.5 → 145.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 2.53 | 14.01 | 99% | 0.71 | 1.96 | 4.34 | 151.4 → 148.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.07 | 3.47 | 17.46 | 99% | 0.83 | 2.49 | 4.58 | 154.9 → 151.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.88 | 2.89 | 78.01 | 99% | 0.60 | 1.99 | 4.16 | 114.9 → 112.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.94 | 2.53 | 13.75 | 99% | 0.71 | 1.91 | 4.29 | 154.1 → 151.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 2.42 | 13.31 | 99% | 0.68 | 1.89 | 3.79 | 154.1 → 150.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 1.95 | 10.38 | 99% | 0.54 | 1.55 | 3.34 | 75.3 → 74.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.271 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.16 | 13.45 | 99% | 0.75 | 2.56 | 4.22 | 150.2 → 151.6 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.85 | 5.72 | 24.35 | 96% | 0.64 | 5.02 | 4.38 | 549.7 → 571.2 | 12 | 3.5 / 31 |

