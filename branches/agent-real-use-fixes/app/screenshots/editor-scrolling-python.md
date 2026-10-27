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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 2.67 | 12.04 | 99% | 0.58 | 2.11 | 4.99 | 131.7 → 138.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.11 | 2.93 | 16.08 | 99% | 0.81 | 2.24 | 5.25 | 168.4 → 165.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.41 | 4.81 | 17.79 | 99% | 2.55 | 2.79 | 5.69 | 215.6 → 201.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 2.32 | 14.71 | 99% | 0.62 | 1.87 | 4.94 | 152.8 → 154.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.18 | 13.40 | 99% | 0.59 | 1.74 | 4.85 | 127.5 → 126.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.33 | 14.45 | 99% | 0.65 | 1.84 | 4.84 | 152.3 → 148.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 2.85 | 13.70 | 99% | 0.75 | 2.10 | 5.10 | 154.8 → 151.9 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 3.19 | 18.08 | 99% | 0.83 | 2.43 | 5.44 | 158.3 → 154.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 2.22 | 13.39 | 99% | 0.51 | 1.82 | 4.62 | 117.0 → 115.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.36 | 12.66 | 99% | 0.66 | 1.93 | 4.93 | 157.1 → 154.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 2.28 | 14.48 | 99% | 0.64 | 1.85 | 4.30 | 156.7 → 153.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.60 | 2.71 | 11.83 | 99% | 0.60 | 1.84 | 4.33 | 78.7 → 78.2 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable Track | 0.055 |
| AttachingStyle - | 0.034 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.51 | 15.02 | 99% | 0.65 | 2.00 | 4.92 | 153.0 → 154.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 5.35 | 24.48 | 96% | 0.79 | 4.47 | 5.31 | 561.9 → 577.5 | 13 | 3.5 / 31 |

