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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.15 | 9.90 | 99% | 0.48 | 1.72 | 3.85 | 128.6 → 135.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.27 | 13.32 | 99% | 0.65 | 1.74 | 4.08 | 164.5 → 162.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 2.80 | 14.13 | 99% | 0.84 | 2.08 | 4.30 | 210.7 → 196.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.16 | 13.82 | 99% | 0.65 | 2.53 | 4.38 | 150.2 → 151.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.90 | 14.39 | 99% | 0.53 | 1.47 | 3.94 | 124.0 → 122.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 2.08 | 13.65 | 99% | 0.57 | 1.61 | 4.01 | 148.9 → 145.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.23 | 15.24 | 99% | 0.56 | 1.70 | 4.36 | 151.1 → 148.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.16 | 12.48 | 99% | 0.56 | 1.64 | 4.07 | 154.1 → 150.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 3.00 | 13.73 | 99% | 0.53 | 2.41 | 4.01 | 113.8 → 112.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.38 | 14.35 | 99% | 0.61 | 1.82 | 4.20 | 154.3 → 150.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.03 | 13.52 | 99% | 0.57 | 1.60 | 3.67 | 153.4 → 150.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.59 | 1.98 | 100% | 0.41 | 1.32 | 3.24 | 74.5 → 74.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.193 |
| ArrangingLayoutable TextView | 0.064 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.09 | 13.74 | 99% | 0.55 | 1.63 | 4.06 | 149.7 → 150.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.91 | 7.97 | 83.51 | 95% | 2.43 | 6.76 | 4.65 | 551.4 → 570.5 | 13 | 3.5 / 31 |

