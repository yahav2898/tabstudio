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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.10 | 17.40 | 99% | 0.54 | 2.71 | 4.71 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.48 | 17.80 | 99% | 0.93 | 2.84 | 4.95 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.86 | 21.01 | 99% | 1.79 | 2.89 | 5.97 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 3.76 | 17.55 | 99% | 1.75 | 2.93 | 5.34 | 114.9 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.33 | 18.06 | 99% | 0.75 | 2.68 | 5.18 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 3.34 | 21.60 | 99% | 0.83 | 2.76 | 4.85 | 115.0 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.25 | 21.34 | 99% | 0.82 | 2.72 | 5.40 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.94 | 4.58 | 19.93 | 99% | 1.73 | 3.01 | 5.52 | 119.8 → 115.9 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.66 | 3.25 | 15.28 | 99% | 0.56 | 2.75 | 4.49 | 80.3 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.80 | 3.40 | 17.93 | 100% | 0.88 | 2.82 | 5.27 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 3.34 | 16.79 | 99% | 0.78 | 2.83 | 4.53 | 119.7 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.84 | 13.36 | 100% | 0.65 | 2.49 | 4.24 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.345 |
| AttachingStyle - | 0.114 |
| ArrangingLayoutable CachedLineNumberMargin | 0.033 |
| FindingResource - | 0.025 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable LineDrawing | 0.011 |
| MeasuringLayoutable ScrollBar | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.38 | 37.94 | 99% | 0.70 | 2.83 | 4.89 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.60 | 8.88 | 34.72 | 94% | 1.61 | 7.51 | 9.44 | 598.3 → 625.2 | 15 | 3.5 / 30 |

