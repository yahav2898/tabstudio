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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.12 | 11.76 | 99% | 0.45 | 1.75 | 4.51 | 126.4 → 133.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.39 | 15.20 | 99% | 0.69 | 1.82 | 5.05 | 162.6 → 160.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.23 | 4.25 | 15.06 | 99% | 2.48 | 2.76 | 5.68 | 209.3 → 195.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 2.28 | 12.90 | 99% | 0.63 | 1.82 | 4.83 | 148.0 → 149.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.33 | 14.25 | 99% | 0.66 | 1.75 | 4.91 | 122.8 → 121.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.17 | 16.69 | 99% | 0.63 | 1.74 | 4.80 | 147.1 → 143.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 2.38 | 15.43 | 99% | 0.67 | 1.81 | 4.85 | 149.6 → 146.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 4.13 | 86.20 | 99% | 0.75 | 2.67 | 5.27 | 153.1 → 149.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.07 | 10.26 | 99% | 0.56 | 1.72 | 4.93 | 112.3 → 110.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 2.46 | 22.65 | 99% | 0.72 | 1.92 | 5.05 | 152.5 → 149.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.24 | 15.11 | 99% | 0.61 | 1.77 | 4.33 | 152.1 → 149.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.17 | 13.12 | 100% | 0.50 | 1.67 | 4.14 | 73.2 → 73.1 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.396 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable TextView | 0.028 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable WorkspaceView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.21 | 17.34 | 99% | 0.61 | 1.82 | 4.75 | 148.2 → 149.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 5.42 | 24.13 | 96% | 0.75 | 4.32 | 5.26 | 548.1 → 568.7 | 12 | 3.5 / 31 |

