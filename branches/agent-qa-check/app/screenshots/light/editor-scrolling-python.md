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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.00 | 3.27 | 11.22 | 100% | 0.64 | 2.69 | 6.18 | 95.4 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.42 | 22.53 | 99% | 1.06 | 2.66 | 6.00 | 131.3 → 129.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.27 | 3.90 | 23.52 | 99% | 1.60 | 2.74 | 6.96 | 178.0 → 163.4 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.16 | 4.08 | 19.93 | 99% | 2.37 | 2.73 | 6.58 | 116.6 → 118.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.17 | 8.72 | 100% | 0.92 | 2.51 | 6.64 | 91.9 → 90.0 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 3.34 | 26.23 | 99% | 0.99 | 2.65 | 6.49 | 116.5 → 112.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.16 | 4.22 | 21.91 | 100% | 1.54 | 2.70 | 6.86 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.36 | 22.15 | 99% | 0.96 | 2.61 | 6.33 | 121.5 → 118.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 3.17 | 20.08 | 100% | 0.71 | 2.57 | 6.13 | 81.4 → 79.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.14 | 3.60 | 22.35 | 100% | 1.51 | 2.71 | 6.72 | 121.2 → 118.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 3.41 | 26.74 | 100% | 1.32 | 2.70 | 6.68 | 121.4 → 117.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.80 | 19.89 | 100% | 0.66 | 2.25 | 5.57 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.320 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable LineDrawing | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 3.79 | 22.22 | 99% | 1.60 | 2.77 | 6.82 | 117.3 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.65 | 7.01 | 38.94 | 97% | 1.07 | 6.11 | 12.13 | 616.2 → 633.1 | 10 | 3.5 / 31 |

