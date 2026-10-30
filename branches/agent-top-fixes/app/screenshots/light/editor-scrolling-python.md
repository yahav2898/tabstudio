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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.56 | 49.99 | 100% | 0.48 | 2.16 | 5.04 | 95.3 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.81 | 4.70 | 100% | 0.95 | 2.20 | 4.93 | 131.2 → 129.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 5.08 | 55.01 | 100% | 2.90 | 2.64 | 6.18 | 178.0 → 163.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.57 | 4.02 | 100% | 0.70 | 2.12 | 5.45 | 116.5 → 118.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.51 | 3.49 | 100% | 0.73 | 2.03 | 5.61 | 91.9 → 89.9 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.54 | 46.23 | 100% | 0.73 | 2.05 | 5.24 | 116.4 → 112.3 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 3.07 | 5.72 | 100% | 1.32 | 2.24 | 5.54 | 118.5 → 115.1 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 3.14 | 29.48 | 100% | 0.82 | 2.36 | 5.81 | 121.5 → 117.9 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.43 | 3.34 | 100% | 0.54 | 2.03 | 5.25 | 81.3 → 79.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.53 | 43.98 | 100% | 0.75 | 2.01 | 5.47 | 121.2 → 118.0 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.48 | 3.83 | 100% | 0.69 | 2.04 | 4.83 | 121.3 → 117.8 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.46 | 4.33 | 100% | 1.09 | 1.82 | 4.51 | 43.3 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.236 |
| ArrangingLayoutable Track | 0.063 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 2.60 | 3.65 | 100% | 0.68 | 2.14 | 5.24 | 117.2 → 117.9 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.61 | 5.34 | 62.98 | 99% | 0.81 | 4.63 | 9.38 | 608.6 → 631.9 | 3 | 3.5 / 31 |

