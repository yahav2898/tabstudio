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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.37 | 1.41 | 10.42 | 99% | 0.35 | 1.17 | 2.88 | 127.5 → 134.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 1.41 | 11.19 | 99% | 0.38 | 1.15 | 2.72 | 163.9 → 161.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 1.57 | 11.39 | 99% | 0.53 | 1.21 | 2.84 | 210.8 → 196.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 1.40 | 11.33 | 99% | 0.38 | 1.13 | 2.72 | 149.1 → 150.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.34 | 1.27 | 9.76 | 99% | 0.39 | 1.06 | 2.66 | 123.5 → 121.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 1.34 | 12.28 | 99% | 0.38 | 1.12 | 2.69 | 148.4 → 144.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.44 | 2.29 | 11.42 | 99% | 0.43 | 1.28 | 2.74 | 150.5 → 148.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 3.56 | 10.21 | 99% | 2.20 | 2.48 | 2.99 | 154.1 → 151.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 1.26 | 7.06 | 100% | 0.30 | 1.07 | 2.39 | 112.6 → 110.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.39 | 1.31 | 9.86 | 99% | 0.37 | 1.10 | 2.63 | 153.2 → 150.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.39 | 1.36 | 9.81 | 100% | 0.38 | 1.10 | 2.40 | 152.8 → 150.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.07 | 7.81 | 100% | 0.25 | 0.93 | 2.11 | 73.4 → 72.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.152 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable Track | 0.004 |
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
| this editor | 368 | 0.42 | 1.38 | 7.33 | 100% | 0.37 | 1.12 | 2.65 | 149.3 → 150.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 5.20 | 21.06 | 96% | 2.36 | 4.41 | 3.19 | 554.0 → 575.5 | 13 | 3.5 / 31 |

