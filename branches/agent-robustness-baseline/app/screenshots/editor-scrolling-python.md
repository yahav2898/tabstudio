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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.85 | 15.56 | 99% | 0.40 | 2.56 | 4.51 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.20 | 16.12 | 100% | 1.24 | 2.67 | 5.09 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 4.67 | 81.54 | 99% | 2.28 | 2.86 | 5.69 | 178.8 → 164.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.08 | 15.08 | 99% | 0.70 | 2.60 | 4.86 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.01 | 15.34 | 99% | 0.68 | 2.56 | 4.97 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 3.10 | 18.37 | 99% | 1.37 | 2.56 | 4.78 | 116.8 → 112.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 3.77 | 15.57 | 99% | 1.37 | 2.78 | 5.16 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.04 | 13.74 | 99% | 0.69 | 2.59 | 4.83 | 121.8 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.80 | 12.23 | 99% | 0.46 | 2.48 | 4.66 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 3.18 | 17.87 | 99% | 1.08 | 2.64 | 4.77 | 121.6 → 118.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 4.29 | 17.25 | 99% | 1.38 | 2.79 | 4.66 | 121.7 → 118.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.57 | 7.35 | 100% | 0.45 | 2.29 | 4.01 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| AttachingStyle - | 0.068 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.01 | 13.62 | 99% | 0.66 | 2.59 | 4.50 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.33 | 11.71 | 85.90 | 93% | 1.69 | 7.49 | 9.01 | 620.6 → 639.1 | 15 | 3.5 / 31 |

