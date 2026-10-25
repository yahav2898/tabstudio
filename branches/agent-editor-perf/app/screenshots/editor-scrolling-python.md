# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
- 0 classified spans, 0 diagnostics, 683 folds, 0 breakpoints, 0 Live Unit Testing markers.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.30 | 1.13 | 4.26 | 100% | 0.29 | 0.94 | 2.61 | 83.9 → 95.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.39 | 1.52 | 11.68 | 100% | 0.52 | 1.09 | 3.00 | 122.7 → 120.4 | 7 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.58 | 3.32 | 9.58 | 99% | 1.05 | 1.29 | 3.34 | 170.3 → 154.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.38 | 1.56 | 9.52 | 100% | 0.45 | 1.12 | 2.88 | 106.2 → 106.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.29 | 1.29 | 8.23 | 100% | 0.41 | 0.93 | 2.75 | 81.1 → 77.8 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.32 | 1.21 | 6.15 | 100% | 0.36 | 0.92 | 2.61 | 106.0 → 100.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.32 | 1.43 | 7.29 | 100% | 0.39 | 0.99 | 2.68 | 108.3 → 103.5 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.34 | 1.21 | 8.95 | 99% | 0.38 | 0.97 | 2.66 | 111.1 → 106.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.26 | 1.07 | 6.91 | 100% | 0.29 | 0.87 | 2.45 | 70.4 → 67.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.36 | 1.77 | 9.29 | 99% | 0.53 | 1.22 | 3.14 | 111.1 → 106.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.51 | 1.59 | 9.79 | 99% | 0.51 | 1.14 | 2.84 | 110.9 → 106.2 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.15 | 0.91 | 7.39 | 100% | 0.29 | 0.77 | 2.15 | 32.2 → 29.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.134 |
| MeasuringLayoutable LineDrawing | 0.024 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable LineDrawing | 0.015 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.36 | 1.47 | 9.30 | 100% | 0.43 | 1.07 | 2.86 | 106.8 → 106.3 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 7.35 | 30.63 | 95% | 0.56 | 4.74 | 5.41 | 566.0 → 581.0 | 16 | 3.5 / 31 |

