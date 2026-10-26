# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 2.81 | 11.48 | 99% | 0.54 | 2.31 | 3.54 | 121.9 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.00 | 15.02 | 99% | 0.78 | 2.35 | 4.07 | 158.1 → 155.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 3.15 | 14.49 | 99% | 0.91 | 2.41 | 3.94 | 204.1 → 189.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.12 | 4.81 | 15.99 | 99% | 0.83 | 4.13 | 3.91 | 143.6 → 145.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 2.82 | 12.25 | 99% | 0.72 | 2.19 | 4.00 | 118.3 → 116.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.83 | 16.71 | 99% | 0.72 | 2.22 | 3.80 | 143.1 → 139.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 2.78 | 15.33 | 99% | 0.70 | 2.26 | 4.10 | 145.4 → 142.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.00 | 2.59 | 11.53 | 99% | 0.70 | 1.99 | 3.87 | 148.0 → 144.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.91 | 3.03 | 11.32 | 99% | 0.69 | 2.47 | 3.44 | 107.7 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 2.76 | 11.59 | 99% | 0.68 | 2.20 | 3.67 | 147.6 → 144.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.93 | 2.79 | 15.39 | 99% | 0.65 | 2.24 | 3.13 | 147.2 → 144.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.45 | 10.05 | 100% | 0.55 | 1.97 | 2.89 | 68.9 → 68.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.292 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 3.07 | 17.68 | 99% | 0.74 | 2.35 | 3.72 | 143.7 → 144.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.40 | 6.43 | 87.57 | 96% | 0.93 | 5.58 | 4.15 | 545.0 → 566.4 | 12 | 3.5 / 31 |

