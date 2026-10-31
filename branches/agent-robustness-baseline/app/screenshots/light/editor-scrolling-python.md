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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.90 | 3.49 | 16.36 | 99% | 0.60 | 3.00 | 5.05 | 96.2 → 103.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 4.65 | 17.05 | 100% | 1.75 | 3.13 | 5.76 | 132.7 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.27 | 3.95 | 19.39 | 99% | 1.68 | 3.07 | 5.78 | 180.0 → 165.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.06 | 3.72 | 14.84 | 100% | 0.88 | 3.06 | 5.62 | 117.8 → 119.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 3.49 | 19.60 | 99% | 0.86 | 2.82 | 5.40 | 92.7 → 90.7 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 5.06 | 12.93 | 99% | 1.69 | 3.00 | 5.64 | 117.7 → 113.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 3.63 | 20.43 | 99% | 1.03 | 3.02 | 5.49 | 119.8 → 116.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.04 | 3.65 | 15.16 | 100% | 0.88 | 2.93 | 5.65 | 122.7 → 119.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.99 | 3.76 | 14.24 | 100% | 1.64 | 2.95 | 5.83 | 81.8 → 79.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 3.75 | 18.67 | 99% | 1.01 | 3.03 | 5.62 | 122.4 → 119.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.72 | 17.02 | 99% | 0.90 | 3.00 | 4.76 | 122.5 → 119.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 3.15 | 14.30 | 100% | 0.72 | 2.62 | 4.50 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.418 |
| AttachingStyle - | 0.037 |
| ArrangingLayoutable TextView | 0.035 |
| ArrangingLayoutable NumberDrawing | 0.029 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 4.03 | 15.67 | 99% | 1.74 | 3.12 | 5.87 | 118.5 → 119.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.47 | 8.20 | 28.27 | 95% | 1.45 | 6.37 | 9.98 | 620.1 → 637.4 | 14 | 3.5 / 31 |

