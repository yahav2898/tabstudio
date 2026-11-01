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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.31 | 18.91 | 99% | 0.55 | 2.89 | 4.66 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.70 | 18.10 | 99% | 1.04 | 2.97 | 4.93 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.14 | 3.97 | 17.51 | 99% | 1.91 | 2.97 | 5.76 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 4.86 | 19.39 | 99% | 2.40 | 3.08 | 5.88 | 115.2 → 116.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 3.33 | 14.48 | 99% | 0.76 | 2.75 | 5.43 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.84 | 3.53 | 21.41 | 100% | 0.79 | 2.86 | 5.40 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.90 | 3.60 | 15.62 | 100% | 0.83 | 2.94 | 5.25 | 117.5 → 113.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.86 | 3.50 | 19.20 | 99% | 0.82 | 2.85 | 5.38 | 120.2 → 116.2 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.82 | 4.59 | 18.31 | 99% | 1.65 | 2.86 | 4.77 | 80.8 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.87 | 3.52 | 17.47 | 99% | 0.78 | 2.85 | 5.69 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.83 | 3.43 | 16.89 | 100% | 0.78 | 2.80 | 4.84 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.85 | 16.25 | 100% | 0.59 | 2.47 | 4.36 | 44.0 → 43.0 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.270 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.59 | 42.63 | 99% | 0.84 | 2.96 | 5.41 | 116.4 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.37 | 7.93 | 30.07 | 95% | 1.02 | 6.97 | 9.46 | 605.9 → 630.9 | 14 | 3.5 / 30 |

