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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 3.19 | 16.19 | 99% | 0.55 | 2.77 | 4.86 | 96.0 → 103.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.61 | 16.93 | 99% | 1.45 | 2.85 | 5.10 | 132.5 → 130.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 4.52 | 16.96 | 99% | 1.92 | 2.97 | 5.90 | 179.9 → 165.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.23 | 16.10 | 99% | 0.80 | 2.71 | 5.01 | 117.6 → 119.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 3.40 | 16.08 | 99% | 0.86 | 2.67 | 5.19 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.82 | 4.54 | 14.93 | 99% | 1.75 | 2.93 | 5.42 | 117.5 → 113.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 3.32 | 15.83 | 99% | 1.05 | 2.77 | 5.33 | 119.7 → 116.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.25 | 15.75 | 99% | 0.77 | 2.73 | 5.20 | 122.6 → 119.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.92 | 11.42 | 100% | 0.75 | 2.55 | 4.76 | 81.7 → 79.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 5.27 | 77.48 | 99% | 2.44 | 3.17 | 5.55 | 122.3 → 119.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.41 | 16.37 | 99% | 1.19 | 2.83 | 4.55 | 122.4 → 118.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.86 | 4.25 | 100% | 0.51 | 2.47 | 4.28 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.298 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.09 | 4.33 | 18.74 | 99% | 1.55 | 3.23 | 6.06 | 118.3 → 119.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.25 | 7.21 | 30.78 | 95% | 1.04 | 6.36 | 9.36 | 618.0 → 637.4 | 14 | 3.5 / 31 |

