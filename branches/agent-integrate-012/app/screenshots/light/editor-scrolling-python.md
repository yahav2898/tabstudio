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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.70 | 13.14 | 99% | 0.42 | 2.27 | 3.74 | 119.0 → 126.6 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.66 | 2.76 | 67.60 | 99% | 0.64 | 1.45 | 4.00 | 155.4 → 152.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 1.87 | 15.15 | 99% | 0.70 | 1.34 | 3.95 | 201.3 → 186.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 1.71 | 12.76 | 99% | 0.50 | 1.31 | 3.86 | 140.1 → 141.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.51 | 9.35 | 100% | 0.47 | 1.21 | 3.74 | 115.1 → 113.5 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 3.79 | 13.91 | 99% | 0.82 | 2.65 | 4.13 | 140.1 → 136.9 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.56 | 1.87 | 12.87 | 99% | 0.50 | 1.43 | 3.84 | 142.1 → 138.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 1.70 | 15.85 | 99% | 0.52 | 1.29 | 3.91 | 145.0 → 141.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.48 | 1.53 | 9.48 | 100% | 0.40 | 1.24 | 3.69 | 104.5 → 102.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 1.78 | 13.42 | 99% | 0.51 | 1.36 | 3.87 | 144.5 → 141.7 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.98 | 66.21 | 99% | 0.66 | 2.42 | 3.78 | 145.3 → 141.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.26 | 7.07 | 100% | 0.35 | 1.06 | 3.18 | 65.9 → 65.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.161 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
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
| this editor | 368 | 0.57 | 1.69 | 13.68 | 99% | 0.50 | 1.31 | 3.87 | 140.7 → 141.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.72 | 6.32 | 23.90 | 96% | 0.70 | 4.76 | 4.08 | 539.9 → 561.9 | 13 | 3.5 / 31 |

