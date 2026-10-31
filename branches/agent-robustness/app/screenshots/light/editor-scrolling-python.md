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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.83 | 16.47 | 99% | 0.40 | 2.56 | 4.65 | 96.1 → 103.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 3.04 | 16.90 | 99% | 0.84 | 2.56 | 4.50 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 4.56 | 20.89 | 99% | 2.51 | 3.00 | 6.00 | 180.0 → 165.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.95 | 15.12 | 99% | 0.71 | 2.57 | 4.66 | 117.7 → 119.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.83 | 16.83 | 99% | 0.66 | 2.42 | 5.04 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 2.91 | 16.26 | 99% | 0.64 | 2.47 | 4.60 | 117.6 → 113.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 5.03 | 18.53 | 99% | 1.64 | 2.87 | 5.04 | 119.8 → 116.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.02 | 13.79 | 99% | 0.66 | 2.60 | 4.64 | 122.6 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.80 | 14.04 | 99% | 0.45 | 2.46 | 4.60 | 81.8 → 79.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.98 | 16.33 | 99% | 0.65 | 2.56 | 4.70 | 122.4 → 119.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 4.21 | 22.08 | 99% | 1.38 | 2.78 | 4.52 | 122.5 → 119.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.59 | 11.39 | 100% | 0.51 | 2.28 | 3.92 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.201 |
| AttachingStyle - | 0.101 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.98 | 18.60 | 99% | 0.60 | 2.55 | 4.60 | 118.3 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.21 | 15.62 | 89.81 | 92% | 2.45 | 12.93 | 9.39 | 614.0 → 639.6 | 15 | 3.5 / 31 |

