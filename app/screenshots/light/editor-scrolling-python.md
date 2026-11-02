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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 3.39 | 12.87 | 100% | 0.54 | 2.90 | 4.77 | 93.3 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.54 | 19.65 | 99% | 0.99 | 2.88 | 4.86 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 4.30 | 21.67 | 99% | 2.11 | 2.94 | 5.88 | 176.2 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 4.66 | 17.14 | 99% | 1.44 | 3.00 | 5.51 | 114.9 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.41 | 17.35 | 100% | 0.87 | 2.73 | 5.43 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.79 | 3.36 | 24.19 | 99% | 0.76 | 2.77 | 5.27 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.98 | 3.70 | 19.03 | 99% | 0.88 | 2.94 | 5.99 | 117.1 → 113.1 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.00 | 4.55 | 95.69 | 99% | 1.66 | 2.99 | 5.59 | 119.9 → 115.8 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.70 | 3.22 | 13.35 | 99% | 0.57 | 2.73 | 4.61 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.96 | 3.62 | 16.42 | 99% | 0.92 | 2.93 | 5.01 | 119.6 → 115.9 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.97 | 3.69 | 20.29 | 99% | 1.09 | 3.02 | 5.15 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 3.23 | 90.49 | 100% | 1.57 | 2.64 | 4.67 | 43.5 → 42.8 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.323 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable TextView | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.90 | 3.46 | 35.92 | 99% | 0.85 | 2.86 | 4.84 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.81 | 10.40 | 107.13 | 90% | 2.65 | 7.52 | 9.84 | 597.8 → 625.1 | 15 | 3.5 / 30 |

