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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.88 | 16.22 | 99% | 0.41 | 2.59 | 4.11 | 94.2 → 101.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.15 | 16.91 | 100% | 0.84 | 2.68 | 5.12 | 131.0 → 131.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 3.48 | 18.12 | 99% | 1.53 | 2.69 | 5.28 | 178.8 → 164.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 4.98 | 20.65 | 99% | 2.56 | 2.83 | 5.53 | 116.3 → 117.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.85 | 17.08 | 99% | 0.69 | 2.44 | 4.97 | 92.2 → 89.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.62 | 3.05 | 21.93 | 99% | 0.69 | 2.59 | 4.49 | 116.6 → 111.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.65 | 3.07 | 11.79 | 100% | 0.64 | 2.57 | 4.39 | 118.5 → 114.5 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 3.32 | 16.42 | 99% | 1.44 | 2.80 | 5.44 | 121.4 → 117.3 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.62 | 3.27 | 15.28 | 99% | 1.46 | 2.66 | 4.84 | 81.2 → 78.8 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.66 | 3.07 | 16.66 | 99% | 0.74 | 2.61 | 5.00 | 121.1 → 117.3 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.64 | 3.05 | 14.66 | 99% | 0.69 | 2.61 | 3.88 | 121.2 → 117.1 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.62 | 11.10 | 100% | 0.48 | 2.33 | 4.06 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| AttachingStyle - | 0.070 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 3.01 | 32.23 | 99% | 0.66 | 2.60 | 4.28 | 117.5 → 118.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.08 | 6.31 | 29.51 | 96% | 0.86 | 5.62 | 8.60 | 606.0 → 637.2 | 14 | 3.5 / 30 |

