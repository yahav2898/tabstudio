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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.31 | 11.08 | 99% | 0.44 | 2.00 | 3.12 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.54 | 2.53 | 17.78 | 99% | 1.08 | 2.02 | 3.29 | 129.2 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.58 | 2.47 | 15.04 | 99% | 1.20 | 2.00 | 3.17 | 176.1 → 162.0 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.44 | 2.30 | 15.34 | 99% | 0.52 | 1.96 | 3.11 | 114.8 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.24 | 15.73 | 99% | 0.51 | 1.92 | 3.13 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.53 | 2.78 | 17.94 | 99% | 1.34 | 2.15 | 3.27 | 115.0 → 110.4 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.68 | 3.54 | 89.16 | 99% | 1.54 | 2.34 | 3.50 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.49 | 2.58 | 12.80 | 99% | 0.61 | 2.02 | 3.46 | 119.8 → 115.8 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.32 | 2.05 | 15.17 | 99% | 0.55 | 1.82 | 2.86 | 80.3 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.38 | 2.12 | 14.19 | 99% | 0.55 | 1.84 | 2.91 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.50 | 2.35 | 14.84 | 100% | 0.59 | 1.97 | 2.78 | 119.7 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.93 | 14.47 | 100% | 0.35 | 1.71 | 2.41 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| ArrangingLayoutable CachedLineNumberMargin | 0.048 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 3.22 | 33.43 | 99% | 1.32 | 2.40 | 3.46 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.78 | 5.00 | 30.52 | 96% | 0.77 | 4.21 | 6.21 | 566.7 → 596.0 | 14 | 3.5 / 30 |

