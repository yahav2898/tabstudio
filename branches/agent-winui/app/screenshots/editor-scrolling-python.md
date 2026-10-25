# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.32 | 1.23 | 9.55 | 100% | 0.23 | 1.08 | 2.49 | 118.2 → 125.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.40 | 1.32 | 9.43 | 99% | 0.34 | 1.09 | 2.63 | 154.4 → 152.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.46 | 1.70 | 14.26 | 99% | 0.56 | 1.16 | 3.05 | 201.1 → 186.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 1.29 | 9.48 | 99% | 0.34 | 1.07 | 2.52 | 139.6 → 141.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.42 | 9.94 | 100% | 0.43 | 1.03 | 2.66 | 114.8 → 113.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.43 | 2.58 | 8.62 | 100% | 0.52 | 1.55 | 2.69 | 139.8 → 135.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 1.28 | 10.22 | 99% | 0.35 | 1.05 | 2.59 | 141.3 → 138.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.33 | 9.77 | 99% | 0.34 | 1.11 | 2.48 | 144.3 → 141.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.31 | 1.22 | 5.77 | 100% | 0.25 | 1.04 | 2.40 | 103.9 → 102.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.31 | 8.65 | 99% | 0.33 | 1.07 | 2.59 | 143.9 → 141.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 2.25 | 9.72 | 100% | 0.37 | 1.25 | 2.25 | 143.8 → 140.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.26 | 2.31 | 9.38 | 100% | 0.38 | 1.14 | 2.11 | 65.8 → 65.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.140 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.38 | 1.34 | 11.89 | 99% | 0.37 | 1.12 | 2.66 | 140.1 → 141.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 2.74 | 16.96 | 96% | 0.39 | 2.47 | 2.76 | 543.8 → 562.3 | 12 | 3.5 / 31 |

