# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 3.54 | 15.56 | 100% | 0.56 | 3.07 | 4.63 | 95.5 → 102.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.89 | 19.02 | 99% | 1.56 | 3.19 | 4.98 | 131.8 → 129.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.48 | 6.29 | 98.38 | 97% | 2.93 | 3.36 | 6.21 | 178.8 → 164.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 3.80 | 21.37 | 99% | 0.76 | 3.15 | 5.38 | 117.0 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.39 | 19.94 | 99% | 0.77 | 2.86 | 5.29 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 3.82 | 19.39 | 99% | 0.85 | 3.06 | 5.45 | 116.9 → 112.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 4.09 | 23.06 | 99% | 2.00 | 3.20 | 5.53 | 119.0 → 115.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.73 | 17.63 | 99% | 0.85 | 3.07 | 5.48 | 121.9 → 118.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.43 | 17.40 | 99% | 0.58 | 2.93 | 5.06 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 3.60 | 18.58 | 99% | 0.81 | 3.06 | 5.30 | 121.6 → 118.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 3.73 | 33.32 | 99% | 1.65 | 3.07 | 4.94 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 3.33 | 91.08 | 99% | 1.78 | 2.82 | 4.60 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.373 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.70 | 19.48 | 99% | 0.76 | 3.09 | 5.02 | 117.6 → 118.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.76 | 9.45 | 35.54 | 92% | 2.04 | 7.72 | 9.75 | 623.5 → 642.1 | 16 | 3.5 / 31 |

