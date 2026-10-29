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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 5.62 | 15.33 | 99% | 0.54 | 4.96 | 6.59 | 134.5 → 142.7 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.31 | 12.47 | 99% | 0.61 | 2.81 | 5.95 | 170.6 → 169.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.52 | 16.23 | 99% | 0.85 | 2.88 | 6.02 | 217.9 → 201.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 3.64 | 17.28 | 99% | 0.70 | 3.09 | 6.40 | 156.8 → 158.1 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.38 | 11.05 | 99% | 0.61 | 2.91 | 5.85 | 131.9 → 129.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.24 | 16.37 | 99% | 0.61 | 2.82 | 5.94 | 155.6 → 152.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.41 | 20.86 | 99% | 0.61 | 2.93 | 6.34 | 157.5 → 154.7 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.98 | 3.71 | 15.98 | 99% | 0.72 | 3.07 | 6.32 | 161.6 → 157.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.11 | 11.49 | 99% | 0.50 | 2.75 | 5.75 | 121.4 → 118.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 3.35 | 13.69 | 99% | 0.61 | 2.83 | 5.92 | 160.2 → 158.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.00 | 5.35 | 15.97 | 99% | 0.77 | 3.26 | 5.95 | 160.5 → 157.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.81 | 12.36 | 99% | 0.44 | 2.49 | 4.79 | 81.0 → 81.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 3.27 | 15.82 | 99% | 0.62 | 2.85 | 6.01 | 156.3 → 157.4 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.04 | 9.42 | 22.63 | 94% | 0.77 | 7.14 | 7.51 | 567.9 → 577.3 | 13 | 3.5 / 31 |

