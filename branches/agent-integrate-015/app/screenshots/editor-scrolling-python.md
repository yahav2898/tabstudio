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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 3.33 | 13.88 | 100% | 0.55 | 2.79 | 5.12 | 126.8 → 134.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.18 | 4.64 | 18.59 | 99% | 0.97 | 3.00 | 5.68 | 164.6 → 161.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.19 | 3.60 | 19.31 | 99% | 1.08 | 2.74 | 5.62 | 210.7 → 195.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 2.85 | 14.56 | 99% | 0.79 | 2.20 | 5.56 | 148.8 → 150.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.95 | 2.93 | 15.36 | 99% | 0.84 | 2.15 | 5.59 | 123.5 → 122.4 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.82 | 18.32 | 99% | 0.98 | 2.64 | 5.74 | 149.4 → 144.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 2.98 | 17.94 | 99% | 0.78 | 2.26 | 5.47 | 150.7 → 147.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.02 | 17.44 | 99% | 0.83 | 2.28 | 5.71 | 153.5 → 150.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.00 | 2.88 | 13.48 | 99% | 0.69 | 2.22 | 5.26 | 113.0 → 111.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.29 | 3.88 | 20.91 | 99% | 1.10 | 2.71 | 5.68 | 154.0 → 150.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.08 | 3.01 | 17.78 | 99% | 0.86 | 2.25 | 4.94 | 153.0 → 149.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 2.23 | 11.24 | 100% | 0.56 | 1.73 | 4.25 | 74.5 → 73.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.252 |
| ArrangingLayoutable NumberDrawing | 0.057 |
| MeasuringLayoutable Grid | 0.048 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 2.39 | 19.00 | 100% | 0.70 | 1.90 | 5.14 | 148.7 → 149.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.71 | 9.39 | 26.54 | 94% | 0.86 | 7.84 | 6.10 | 557.6 → 576.1 | 13 | 3.5 / 31 |

