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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.89 | 3.46 | 16.42 | 100% | 0.62 | 2.93 | 5.14 | 93.3 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 3.74 | 20.12 | 99% | 1.60 | 2.96 | 5.60 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.35 | 4.08 | 20.32 | 99% | 2.02 | 3.03 | 6.16 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.20 | 4.75 | 116.02 | 99% | 2.71 | 3.03 | 5.99 | 114.8 → 115.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.82 | 3.44 | 18.61 | 99% | 0.97 | 2.72 | 5.52 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 3.39 | 17.96 | 99% | 0.78 | 2.75 | 4.80 | 115.0 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.84 | 3.44 | 29.74 | 99% | 0.81 | 2.78 | 5.35 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.89 | 3.57 | 17.22 | 99% | 0.85 | 2.80 | 5.44 | 119.8 → 115.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.88 | 4.25 | 15.03 | 99% | 1.72 | 2.84 | 5.75 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.94 | 3.47 | 13.53 | 100% | 0.81 | 2.80 | 5.33 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.91 | 3.42 | 18.04 | 99% | 0.88 | 2.84 | 4.83 | 119.7 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.81 | 15.36 | 100% | 0.55 | 2.41 | 4.15 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.328 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 3.87 | 47.77 | 99% | 1.78 | 3.11 | 5.79 | 116.1 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.55 | 9.33 | 34.12 | 95% | 1.06 | 6.40 | 9.86 | 599.7 → 625.0 | 14 | 3.5 / 30 |

