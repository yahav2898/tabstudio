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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.80 | 10.64 | 99% | 0.41 | 2.53 | 4.29 | 128.8 → 136.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.23 | 16.75 | 99% | 0.66 | 2.56 | 4.85 | 165.2 → 163.6 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 3.94 | 80.30 | 98% | 1.08 | 2.86 | 5.28 | 213.3 → 197.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.70 | 12.85 | 99% | 0.60 | 2.36 | 4.71 | 150.6 → 152.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.63 | 13.31 | 99% | 0.58 | 2.30 | 4.81 | 125.5 → 123.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 3.04 | 11.35 | 99% | 0.66 | 2.54 | 4.74 | 150.2 → 146.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 3.74 | 13.69 | 99% | 0.71 | 2.80 | 5.05 | 153.4 → 148.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.78 | 13.65 | 99% | 0.62 | 2.32 | 4.65 | 155.0 → 151.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.66 | 18.59 | 99% | 0.45 | 2.35 | 4.14 | 114.9 → 113.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 3.16 | 17.33 | 99% | 0.65 | 2.62 | 4.94 | 154.9 → 152.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.66 | 75.83 | 99% | 0.65 | 2.69 | 4.43 | 155.3 → 151.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.41 | 8.18 | 100% | 0.48 | 2.14 | 4.17 | 75.9 → 75.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.89 | 14.36 | 99% | 0.60 | 2.47 | 4.79 | 151.0 → 151.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.14 | 7.77 | 85.95 | 95% | 0.82 | 7.19 | 5.38 | 563.6 → 572.7 | 13 | 3.5 / 31 |

