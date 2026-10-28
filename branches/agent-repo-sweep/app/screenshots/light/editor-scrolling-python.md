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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.77 | 16.76 | 99% | 0.48 | 2.39 | 3.91 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.97 | 19.76 | 100% | 0.84 | 2.42 | 4.40 | 131.2 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 3.44 | 17.47 | 99% | 1.67 | 2.54 | 4.75 | 177.9 → 163.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.11 | 18.05 | 99% | 1.28 | 2.54 | 4.57 | 116.5 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.72 | 3.09 | 14.71 | 99% | 1.68 | 2.48 | 4.65 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.19 | 19.85 | 99% | 0.95 | 2.58 | 4.51 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 3.14 | 19.29 | 99% | 0.78 | 2.54 | 4.36 | 118.5 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 3.26 | 17.35 | 99% | 0.78 | 2.61 | 4.42 | 121.4 → 117.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.19 | 17.53 | 100% | 1.52 | 2.52 | 4.38 | 81.3 → 79.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 3.18 | 16.59 | 99% | 0.75 | 2.60 | 4.44 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 3.23 | 19.29 | 99% | 0.88 | 2.59 | 4.09 | 121.3 → 117.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.71 | 11.91 | 100% | 0.63 | 2.30 | 4.05 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.277 |
| ArrangingLayoutable LineDrawing | 0.065 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.95 | 4.37 | 90.06 | 99% | 1.84 | 2.74 | 5.00 | 117.2 → 118.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.20 | 7.15 | 29.49 | 95% | 1.25 | 5.80 | 8.33 | 612.7 → 634.1 | 14 | 3.5 / 31 |

