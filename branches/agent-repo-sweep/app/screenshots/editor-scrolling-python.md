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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 2.08 | 14.35 | 99% | 0.35 | 1.83 | 3.52 | 95.6 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.20 | 15.73 | 99% | 1.33 | 1.65 | 4.27 | 131.7 → 129.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 2.47 | 16.12 | 99% | 1.64 | 1.96 | 4.42 | 178.7 → 164.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 4.32 | 75.23 | 99% | 2.42 | 2.36 | 4.40 | 116.9 → 118.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.15 | 11.64 | 99% | 1.36 | 1.84 | 4.00 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 2.02 | 13.81 | 99% | 0.56 | 1.64 | 3.99 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.49 | 15.00 | 99% | 0.73 | 1.97 | 4.52 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.45 | 16.06 | 99% | 0.61 | 1.98 | 4.13 | 121.8 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.44 | 2.20 | 11.87 | 99% | 1.07 | 1.83 | 3.79 | 81.7 → 79.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 3.75 | 15.63 | 99% | 1.63 | 2.45 | 4.23 | 121.6 → 118.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.15 | 14.46 | 99% | 1.14 | 1.69 | 3.52 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 1.84 | 7.65 | 100% | 0.37 | 1.63 | 3.21 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.184 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| this editor | 368 | 0.60 | 2.17 | 14.73 | 99% | 0.58 | 1.74 | 3.88 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.29 | 9.81 | 28.32 | 94% | 2.43 | 7.40 | 7.90 | 601.7 → 634.8 | 15 | 3.5 / 31 |

