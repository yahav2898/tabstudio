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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 2.08 | 15.85 | 100% | 0.31 | 1.85 | 3.01 | 94.2 → 101.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.28 | 13.99 | 99% | 0.92 | 1.89 | 2.92 | 131.0 → 131.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 2.31 | 15.33 | 99% | 1.01 | 1.92 | 3.11 | 178.8 → 164.5 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.38 | 2.07 | 13.18 | 99% | 0.40 | 1.84 | 2.79 | 116.3 → 117.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.32 | 2.12 | 13.14 | 99% | 0.51 | 1.79 | 2.83 | 92.2 → 89.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.42 | 2.27 | 14.14 | 100% | 0.52 | 1.86 | 2.88 | 116.6 → 111.9 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 2.89 | 19.82 | 99% | 1.41 | 2.28 | 3.21 | 118.5 → 114.4 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.58 | 3.04 | 15.49 | 99% | 1.20 | 2.43 | 3.21 | 121.4 → 117.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.41 | 2.37 | 11.12 | 99% | 0.51 | 1.99 | 3.22 | 81.1 → 78.8 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.47 | 2.26 | 15.13 | 99% | 0.55 | 1.93 | 2.89 | 121.1 → 117.3 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.42 | 2.17 | 14.16 | 99% | 0.54 | 1.85 | 2.65 | 121.2 → 117.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.95 | 11.50 | 100% | 0.95 | 1.71 | 2.40 | 43.9 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.162 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 4.12 | 32.34 | 99% | 2.28 | 2.44 | 3.20 | 117.4 → 118.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.31 | 4.39 | 23.44 | 96% | 0.65 | 3.59 | 5.00 | 573.8 → 601.4 | 14 | 3.5 / 30 |

