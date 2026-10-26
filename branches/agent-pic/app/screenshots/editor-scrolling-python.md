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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.96 | 10.10 | 100% | 0.38 | 1.68 | 4.11 | 122.1 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 4.03 | 17.43 | 99% | 0.81 | 2.59 | 5.09 | 158.5 → 156.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.47 | 16.15 | 99% | 0.86 | 1.85 | 4.96 | 205.1 → 190.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.20 | 15.08 | 99% | 0.58 | 1.76 | 4.84 | 143.7 → 145.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 1.89 | 11.49 | 99% | 0.55 | 1.52 | 4.64 | 118.5 → 117.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.98 | 16.67 | 99% | 0.68 | 2.36 | 5.15 | 143.6 → 139.8 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.55 | 71.37 | 99% | 0.61 | 1.89 | 4.93 | 145.6 → 142.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.21 | 14.17 | 99% | 0.60 | 1.71 | 4.60 | 148.2 → 145.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.97 | 14.91 | 99% | 0.44 | 1.62 | 4.59 | 107.9 → 106.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.67 | 12.97 | 99% | 0.62 | 2.00 | 4.81 | 148.0 → 145.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.79 | 13.83 | 99% | 0.63 | 1.92 | 4.45 | 148.6 → 144.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.64 | 10.70 | 99% | 0.42 | 1.38 | 3.84 | 69.1 → 68.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.229 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.14 | 14.88 | 99% | 0.56 | 1.71 | 4.64 | 144.1 → 145.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.90 | 7.63 | 29.95 | 95% | 2.33 | 6.24 | 5.44 | 554.8 → 568.9 | 13 | 3.5 / 31 |

