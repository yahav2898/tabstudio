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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 3.00 | 13.55 | 100% | 0.45 | 2.57 | 4.55 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 4.23 | 17.10 | 100% | 1.60 | 2.83 | 5.12 | 129.2 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 3.51 | 17.84 | 99% | 1.55 | 2.66 | 5.25 | 176.1 → 162.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 3.04 | 17.78 | 99% | 0.64 | 2.58 | 5.04 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.90 | 30.46 | 99% | 0.67 | 2.48 | 4.94 | 91.1 → 88.9 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 3.23 | 73.02 | 99% | 1.28 | 2.68 | 5.01 | 115.1 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.63 | 3.02 | 14.23 | 100% | 0.70 | 2.56 | 4.46 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.03 | 19.11 | 99% | 0.73 | 2.58 | 4.64 | 119.8 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 3.17 | 11.39 | 99% | 1.40 | 2.71 | 4.79 | 80.2 → 78.0 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.66 | 3.08 | 12.17 | 100% | 0.67 | 2.59 | 4.82 | 119.5 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 3.06 | 18.11 | 99% | 0.98 | 2.61 | 4.61 | 119.6 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.66 | 4.10 | 100% | 1.03 | 2.33 | 4.10 | 43.5 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.353 |
| AttachingStyle - | 0.104 |
| ArrangingLayoutable NumberDrawing | 0.037 |
| ArrangingLayoutable TextView | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable RepeatButton | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 3.04 | 33.26 | 99% | 0.68 | 2.60 | 4.44 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.30 | 13.52 | 40.22 | 93% | 1.71 | 9.04 | 8.96 | 601.2 → 626.1 | 15 | 3.5 / 30 |

