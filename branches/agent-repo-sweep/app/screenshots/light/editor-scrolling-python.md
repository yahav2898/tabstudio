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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.56 | 19.16 | 99% | 0.61 | 2.99 | 4.59 | 96.1 → 103.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 3.80 | 18.77 | 99% | 1.57 | 2.95 | 4.90 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 5.65 | 21.60 | 99% | 3.03 | 2.96 | 5.52 | 180.0 → 165.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.98 | 3.66 | 18.91 | 99% | 0.80 | 2.92 | 5.22 | 117.7 → 119.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 3.41 | 22.17 | 99% | 0.86 | 2.78 | 4.96 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 3.42 | 21.75 | 100% | 0.86 | 2.80 | 4.92 | 117.6 → 113.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.13 | 4.93 | 21.07 | 99% | 2.18 | 3.14 | 7.01 | 119.7 → 116.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.58 | 17.53 | 99% | 1.47 | 2.86 | 4.93 | 122.6 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 3.19 | 15.00 | 99% | 0.69 | 2.68 | 4.72 | 81.8 → 79.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.90 | 3.22 | 18.95 | 99% | 0.80 | 2.69 | 4.69 | 122.4 → 119.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 3.49 | 31.38 | 99% | 0.99 | 2.85 | 4.24 | 122.5 → 118.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 3.30 | 88.45 | 99% | 1.76 | 2.70 | 4.18 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| ArrangingLayoutable TextView | 0.080 |
| FindingResource - | 0.016 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 3.47 | 19.26 | 99% | 0.75 | 2.82 | 4.90 | 118.4 → 119.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.66 | 8.64 | 29.99 | 94% | 2.14 | 7.62 | 9.45 | 616.6 → 639.6 | 15 | 3.5 / 31 |

