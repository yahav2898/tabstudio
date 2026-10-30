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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.06 | 3.71 | 18.35 | 99% | 0.66 | 3.09 | 5.29 | 96.4 → 103.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.25 | 3.93 | 18.79 | 99% | 1.42 | 3.09 | 5.88 | 133.1 → 130.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.60 | 5.69 | 22.24 | 98% | 2.64 | 3.32 | 6.31 | 180.7 → 165.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.19 | 3.87 | 21.89 | 99% | 0.97 | 3.11 | 5.88 | 118.1 → 119.5 | 2 | 1.1 / 4 |
| without line numbers | 368 | 1.02 | 3.66 | 17.29 | 99% | 1.05 | 2.86 | 5.77 | 93.1 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.16 | 3.81 | 20.72 | 99% | 1.82 | 2.98 | 6.08 | 118.1 → 113.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.23 | 4.77 | 104.38 | 99% | 1.68 | 3.08 | 5.99 | 120.3 → 116.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.20 | 3.98 | 18.33 | 99% | 0.99 | 3.17 | 5.81 | 123.1 → 119.5 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.64 | 14.95 | 99% | 0.74 | 2.99 | 5.58 | 82.3 → 80.2 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 4.44 | 92.56 | 99% | 2.05 | 3.18 | 6.01 | 122.9 → 119.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.23 | 3.76 | 18.48 | 99% | 1.41 | 3.05 | 5.12 | 123.0 → 119.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 3.25 | 11.58 | 100% | 0.68 | 2.65 | 4.71 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.348 |
| RaisingRoutedEvent ScrollBar | 0.071 |
| ArrangingLayoutable TextView | 0.026 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.016 |
| ArrangingLayoutable LineDrawing | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.014 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.25 | 4.16 | 17.80 | 99% | 1.80 | 3.15 | 5.97 | 118.8 → 119.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 5.21 | 9.92 | 29.84 | 94% | 1.80 | 6.79 | 10.34 | 626.5 → 644.8 | 14 | 3.5 / 31 |

