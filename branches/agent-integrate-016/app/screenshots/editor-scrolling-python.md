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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 1.70 | 10.78 | 99% | 0.40 | 1.38 | 3.10 | 127.3 → 134.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 1.85 | 13.81 | 99% | 0.53 | 1.43 | 3.09 | 163.7 → 161.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.64 | 2.04 | 13.01 | 99% | 0.67 | 1.49 | 3.19 | 210.3 → 195.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.70 | 11.95 | 99% | 0.46 | 1.34 | 2.96 | 148.6 → 150.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.67 | 11.87 | 99% | 0.48 | 1.32 | 3.04 | 123.5 → 122.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 2.73 | 15.54 | 99% | 0.48 | 1.51 | 2.94 | 147.9 → 145.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 3.83 | 19.61 | 99% | 0.84 | 2.62 | 5.02 | 151.2 → 148.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.55 | 1.69 | 13.15 | 99% | 0.45 | 1.35 | 2.89 | 153.1 → 150.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.47 | 8.94 | 99% | 0.37 | 1.24 | 2.68 | 112.5 → 111.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 1.85 | 13.39 | 99% | 0.51 | 1.50 | 3.09 | 153.2 → 150.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 1.71 | 11.70 | 99% | 0.47 | 1.34 | 2.65 | 152.8 → 149.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.26 | 6.79 | 100% | 0.34 | 1.07 | 2.33 | 74.3 → 73.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.224 |
| AttachingStyle - | 0.044 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
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
| this editor | 368 | 0.72 | 3.28 | 13.46 | 99% | 1.01 | 2.43 | 3.59 | 149.8 → 150.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 4.90 | 22.99 | 96% | 0.50 | 4.13 | 3.28 | 552.4 → 571.8 | 13 | 3.5 / 31 |

