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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.98 | 14.57 | 100% | 0.46 | 2.66 | 4.34 | 93.4 → 100.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 3.20 | 17.11 | 99% | 1.03 | 2.64 | 4.53 | 129.7 → 130.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 3.75 | 17.86 | 99% | 1.62 | 2.78 | 5.26 | 176.9 → 162.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.48 | 16.67 | 99% | 1.51 | 2.76 | 5.00 | 115.2 → 116.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 4.10 | 17.55 | 99% | 1.82 | 2.66 | 5.72 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.65 | 3.06 | 19.29 | 99% | 0.72 | 2.54 | 4.65 | 115.5 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.67 | 3.18 | 17.77 | 99% | 0.76 | 2.62 | 4.56 | 117.5 → 113.3 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.15 | 17.86 | 99% | 0.70 | 2.65 | 4.71 | 120.2 → 116.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.59 | 3.04 | 14.28 | 100% | 0.53 | 2.57 | 4.47 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.85 | 4.82 | 21.17 | 99% | 2.24 | 2.84 | 5.29 | 120.0 → 116.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.67 | 3.04 | 19.57 | 99% | 0.69 | 2.61 | 4.49 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.72 | 11.98 | 100% | 0.48 | 2.35 | 4.14 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 3.44 | 35.74 | 99% | 1.68 | 2.85 | 4.92 | 116.3 → 117.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.20 | 7.04 | 27.45 | 95% | 0.88 | 5.81 | 8.69 | 604.9 → 632.8 | 14 | 3.5 / 30 |

