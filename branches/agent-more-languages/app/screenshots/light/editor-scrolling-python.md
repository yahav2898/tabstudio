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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 3.48 | 20.01 | 99% | 0.61 | 2.95 | 5.09 | 96.2 → 103.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.19 | 3.94 | 89.52 | 99% | 1.82 | 3.00 | 5.64 | 132.7 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 3.99 | 19.77 | 99% | 1.81 | 3.00 | 5.86 | 179.9 → 165.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.05 | 3.59 | 20.81 | 99% | 0.86 | 2.97 | 5.47 | 117.7 → 119.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 3.76 | 84.84 | 99% | 1.94 | 2.82 | 5.89 | 92.6 → 90.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 3.60 | 20.11 | 99% | 0.90 | 2.89 | 5.58 | 117.6 → 113.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 3.69 | 19.63 | 99% | 1.01 | 2.98 | 5.64 | 119.8 → 116.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.17 | 3.93 | 32.46 | 99% | 1.82 | 3.04 | 6.08 | 122.7 → 119.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 3.71 | 94.07 | 99% | 0.80 | 2.97 | 5.54 | 81.9 → 79.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 3.76 | 17.71 | 99% | 0.90 | 3.03 | 5.48 | 122.4 → 119.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.06 | 3.71 | 17.50 | 99% | 0.94 | 3.02 | 5.12 | 122.5 → 118.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 3.21 | 5.52 | 100% | 1.58 | 2.66 | 4.68 | 43.4 → 42.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.387 |
| AttachingStyle - | 0.085 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| this editor | 368 | 0.87 | 3.59 | 16.48 | 99% | 0.91 | 2.94 | 5.21 | 118.4 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.66 | 15.10 | 35.37 | 88% | 1.95 | 12.01 | 9.95 | 617.9 → 646.4 | 16 | 3.5 / 31 |

