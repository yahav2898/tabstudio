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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.02 | 2.86 | 100% | 0.40 | 1.72 | 4.06 | 96.1 → 103.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.73 | 2.23 | 48.42 | 100% | 0.80 | 1.76 | 4.52 | 132.8 → 130.3 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.61 | 45.13 | 100% | 1.20 | 1.81 | 4.94 | 179.9 → 165.1 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 3.99 | 6.09 | 100% | 2.36 | 2.49 | 4.89 | 117.7 → 119.1 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.98 | 3.01 | 100% | 0.65 | 1.59 | 4.62 | 92.6 → 90.6 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 2.09 | 33.52 | 100% | 0.66 | 1.67 | 4.53 | 117.6 → 113.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 2.20 | 42.04 | 100% | 0.69 | 1.75 | 4.81 | 119.7 → 116.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.26 | 4.21 | 100% | 0.69 | 1.73 | 4.79 | 122.6 → 119.1 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 3.77 | 4.44 | 100% | 1.16 | 2.30 | 4.57 | 81.8 → 79.8 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.24 | 21.76 | 100% | 0.65 | 1.77 | 4.78 | 122.3 → 119.1 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.21 | 43.09 | 100% | 0.68 | 1.74 | 4.32 | 122.4 → 118.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.74 | 2.66 | 100% | 0.46 | 1.47 | 3.83 | 43.4 → 42.7 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.58 | 41.28 | 100% | 1.23 | 1.84 | 4.76 | 118.3 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.17 | 5.01 | 56.13 | 98% | 0.81 | 4.37 | 8.15 | 595.8 → 616.9 | 3 | 3.5 / 31 |

