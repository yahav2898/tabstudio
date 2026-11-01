# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 1.97 | 39.80 | 100% | 0.37 | 1.67 | 4.47 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 2.10 | 44.10 | 100% | 0.75 | 1.66 | 4.41 | 129.3 → 129.8 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 2.33 | 3.46 | 100% | 1.15 | 1.70 | 4.87 | 176.1 → 162.1 | 0 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.07 | 43.67 | 100% | 0.56 | 1.66 | 4.54 | 114.8 → 115.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.36 | 4.13 | 100% | 1.17 | 1.60 | 4.56 | 91.1 → 88.9 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.61 | 3.55 | 22.45 | 100% | 2.35 | 2.26 | 4.77 | 115.1 → 110.4 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.62 | 2.09 | 7.14 | 100% | 0.62 | 1.69 | 4.83 | 117.0 → 113.0 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.04 | 42.21 | 100% | 0.60 | 1.63 | 4.19 | 119.8 → 115.8 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.51 | 1.91 | 2.95 | 100% | 0.44 | 1.57 | 3.69 | 80.3 → 78.0 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.62 | 2.05 | 42.11 | 100% | 0.63 | 1.64 | 4.29 | 119.5 → 115.8 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.62 | 2.76 | 5.17 | 100% | 1.24 | 1.75 | 4.17 | 119.7 → 115.7 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.46 | 91.24 | 100% | 1.22 | 1.51 | 3.84 | 43.6 → 42.7 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.255 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.10 | 3.82 | 100% | 0.60 | 1.70 | 4.76 | 116.0 → 117.1 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.92 | 4.50 | 69.16 | 99% | 0.70 | 3.73 | 7.88 | 572.8 → 598.4 | 2 | 3.5 / 30 |

