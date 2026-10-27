# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.00 | 10.58 | 99% | 0.39 | 1.73 | 4.51 | 131.0 → 138.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.22 | 14.62 | 99% | 0.61 | 1.75 | 4.66 | 167.0 → 164.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.80 | 16.04 | 99% | 0.89 | 1.94 | 5.12 | 213.3 → 199.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.19 | 18.00 | 99% | 0.68 | 2.17 | 5.02 | 152.8 → 153.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.94 | 14.07 | 99% | 0.54 | 1.58 | 4.80 | 127.1 → 125.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.07 | 14.08 | 99% | 0.58 | 1.68 | 4.85 | 151.8 → 148.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.13 | 13.43 | 99% | 0.57 | 1.70 | 4.60 | 153.9 → 150.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 4.01 | 15.78 | 99% | 0.69 | 2.53 | 5.06 | 157.0 → 154.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.12 | 9.93 | 99% | 0.43 | 1.68 | 4.11 | 116.6 → 114.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.16 | 15.30 | 99% | 0.61 | 1.77 | 5.02 | 156.7 → 153.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.10 | 14.33 | 99% | 0.55 | 1.68 | 4.25 | 156.5 → 152.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.68 | 9.32 | 100% | 0.38 | 1.40 | 3.95 | 77.3 → 77.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| AttachingStyle - | 0.089 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 3.90 | 15.01 | 99% | 0.74 | 2.35 | 4.88 | 152.9 → 153.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.09 | 4.82 | 21.31 | 96% | 0.69 | 3.88 | 5.20 | 557.4 → 575.9 | 13 | 3.5 / 31 |

