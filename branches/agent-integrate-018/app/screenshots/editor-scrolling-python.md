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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.08 | 2.86 | 20.21 | 100% | 0.62 | 2.35 | 6.16 | 133.5 → 141.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.38 | 3.74 | 23.16 | 99% | 0.98 | 2.74 | 7.22 | 170.0 → 167.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.51 | 3.74 | 23.71 | 99% | 1.12 | 2.75 | 7.21 | 216.6 → 201.9 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.26 | 3.46 | 22.93 | 99% | 0.83 | 2.70 | 6.77 | 155.3 → 156.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.10 | 3.41 | 20.37 | 99% | 0.85 | 2.59 | 7.06 | 130.5 → 128.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.21 | 3.55 | 22.91 | 99% | 0.89 | 2.69 | 7.37 | 155.2 → 151.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.25 | 3.32 | 21.53 | 99% | 0.82 | 2.66 | 6.35 | 156.8 → 153.8 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.26 | 3.45 | 21.82 | 99% | 0.82 | 2.70 | 6.58 | 159.9 → 156.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.12 | 3.34 | 13.52 | 99% | 0.67 | 2.68 | 6.31 | 119.6 → 117.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 3.40 | 22.14 | 99% | 0.84 | 2.66 | 6.61 | 159.5 → 156.7 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.22 | 3.45 | 22.26 | 99% | 0.83 | 2.71 | 5.82 | 159.4 → 156.3 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.81 | 3.04 | 10.96 | 99% | 0.70 | 2.45 | 5.72 | 79.9 → 79.7 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| ArrangingLayoutable NumberDrawing | 0.071 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable LineDrawing | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.29 | 3.37 | 23.20 | 99% | 0.82 | 2.68 | 6.81 | 155.7 → 156.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.19 | 7.86 | 37.03 | 95% | 1.11 | 7.00 | 7.85 | 563.0 → 589.5 | 10 | 3.5 / 31 |

