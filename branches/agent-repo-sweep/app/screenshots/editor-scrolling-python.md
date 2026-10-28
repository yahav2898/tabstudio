# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 3.38 | 15.77 | 100% | 0.54 | 2.92 | 4.73 | 96.3 → 103.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.55 | 17.46 | 99% | 1.01 | 2.96 | 5.34 | 133.1 → 130.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.44 | 5.41 | 22.67 | 98% | 3.17 | 3.18 | 6.16 | 180.7 → 165.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 3.54 | 21.39 | 99% | 0.78 | 2.93 | 5.81 | 118.1 → 119.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 3.25 | 19.43 | 99% | 0.82 | 2.67 | 5.17 | 93.0 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 3.78 | 25.03 | 99% | 1.53 | 2.97 | 5.33 | 118.0 → 113.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 3.73 | 19.42 | 99% | 1.43 | 3.06 | 5.68 | 120.2 → 116.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 3.65 | 22.67 | 99% | 0.78 | 2.95 | 5.15 | 123.0 → 119.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 3.36 | 16.37 | 100% | 0.56 | 2.85 | 5.06 | 82.2 → 80.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 5.53 | 20.11 | 99% | 1.78 | 3.19 | 5.61 | 122.8 → 119.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.94 | 3.58 | 19.60 | 99% | 0.96 | 2.96 | 4.67 | 122.9 → 119.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.97 | 13.79 | 100% | 0.57 | 2.53 | 4.07 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.324 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 4.40 | 19.16 | 99% | 1.91 | 3.07 | 5.85 | 118.7 → 119.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.26 | 7.54 | 36.38 | 95% | 0.97 | 6.22 | 9.44 | 623.1 → 645.5 | 14 | 3.5 / 31 |

