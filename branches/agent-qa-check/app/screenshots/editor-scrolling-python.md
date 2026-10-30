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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.37 | 14.75 | 99% | 0.41 | 2.02 | 2.89 | 96.3 → 103.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.41 | 15.27 | 99% | 1.02 | 1.99 | 3.10 | 133.1 → 130.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 2.89 | 16.22 | 99% | 1.37 | 2.17 | 3.76 | 180.6 → 165.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.51 | 17.06 | 99% | 0.54 | 2.03 | 3.26 | 118.0 → 119.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 4.36 | 14.50 | 99% | 2.49 | 2.28 | 3.31 | 93.0 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.43 | 14.07 | 99% | 0.58 | 2.01 | 3.10 | 118.0 → 113.8 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 2.56 | 12.86 | 100% | 0.73 | 2.11 | 3.27 | 120.2 → 116.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.85 | 16.53 | 99% | 0.92 | 2.16 | 4.96 | 123.0 → 119.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 2.34 | 9.10 | 99% | 0.84 | 1.99 | 3.01 | 82.2 → 80.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.98 | 14.75 | 100% | 1.20 | 2.22 | 3.33 | 122.8 → 119.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 3.76 | 13.34 | 99% | 2.27 | 2.42 | 3.25 | 122.9 → 119.3 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.14 | 13.42 | 100% | 0.83 | 1.80 | 2.71 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.212 |
| FindingResource - | 0.055 |
| ArrangingLayoutable RepeatButton | 0.050 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.67 | 15.62 | 99% | 0.98 | 2.10 | 3.35 | 118.7 → 119.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.76 | 6.04 | 21.95 | 95% | 0.74 | 4.36 | 6.04 | 592.5 → 615.6 | 14 | 3.5 / 31 |

