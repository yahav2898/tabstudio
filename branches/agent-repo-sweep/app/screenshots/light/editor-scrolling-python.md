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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.28 | 1.18 | 12.38 | 99% | 0.23 | 1.01 | 2.23 | 95.3 → 102.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.36 | 1.65 | 12.93 | 99% | 0.49 | 1.44 | 2.51 | 131.2 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.41 | 1.79 | 12.54 | 99% | 0.77 | 1.46 | 2.74 | 177.9 → 163.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.32 | 1.61 | 11.90 | 99% | 0.48 | 1.43 | 2.61 | 116.5 → 117.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.26 | 1.57 | 10.08 | 99% | 0.38 | 1.38 | 2.59 | 91.9 → 89.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.29 | 1.55 | 11.43 | 99% | 0.37 | 1.37 | 2.53 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.40 | 2.54 | 15.23 | 99% | 1.15 | 1.60 | 2.64 | 118.5 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.40 | 2.88 | 14.60 | 99% | 1.27 | 2.26 | 2.88 | 121.5 → 117.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.27 | 1.62 | 9.05 | 100% | 0.27 | 1.44 | 2.47 | 81.2 → 79.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.32 | 1.63 | 11.46 | 99% | 0.38 | 1.42 | 2.64 | 121.1 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.30 | 1.64 | 11.83 | 99% | 0.35 | 1.47 | 2.30 | 121.3 → 117.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.16 | 1.40 | 1.94 | 100% | 0.22 | 1.30 | 2.05 | 43.3 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.121 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable LineDrawing | 0.004 |
| FindingResource - | 0.004 |
| ArrangingLayoutable Track | 0.003 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.32 | 1.51 | 12.42 | 99% | 0.37 | 1.38 | 2.56 | 117.2 → 117.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 10.12 | 86.53 | 95% | 2.40 | 5.53 | 5.44 | 578.0 → 602.7 | 15 | 3.5 / 31 |

