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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.07 | 13.18 | 100% | 0.46 | 1.70 | 3.88 | 128.9 → 135.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.89 | 2.34 | 17.75 | 99% | 0.63 | 1.80 | 4.20 | 164.6 → 162.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 2.48 | 15.71 | 99% | 0.85 | 1.78 | 4.38 | 210.7 → 195.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.17 | 18.01 | 99% | 0.64 | 2.52 | 4.22 | 149.9 → 151.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.45 | 11.40 | 99% | 0.61 | 1.64 | 4.17 | 124.8 → 122.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.11 | 17.43 | 99% | 0.60 | 1.65 | 4.20 | 149.1 → 145.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.30 | 16.64 | 99% | 0.64 | 1.81 | 4.33 | 151.5 → 148.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.06 | 14.24 | 99% | 0.54 | 1.59 | 4.03 | 154.3 → 150.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.71 | 10.20 | 99% | 0.40 | 1.42 | 3.55 | 113.0 → 111.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 3.89 | 17.66 | 99% | 2.33 | 2.62 | 4.31 | 154.0 → 151.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 1.96 | 15.15 | 99% | 0.52 | 1.55 | 3.61 | 153.2 → 150.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.56 | 10.23 | 100% | 0.38 | 1.29 | 3.29 | 74.7 → 74.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.179 |
| ArrangingLayoutable ScrollingTextLayer | 0.067 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.16 | 17.48 | 99% | 0.57 | 1.78 | 4.13 | 149.7 → 151.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.22 | 6.68 | 31.84 | 96% | 0.77 | 5.82 | 5.34 | 552.2 → 577.2 | 14 | 3.5 / 31 |

