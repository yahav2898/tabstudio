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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.68 | 9.16 | 100% | 0.25 | 1.52 | 2.32 | 126.7 → 134.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 1.85 | 12.19 | 99% | 0.39 | 1.60 | 2.77 | 163.4 → 161.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 1.94 | 11.73 | 99% | 0.53 | 1.62 | 2.81 | 209.9 → 195.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.38 | 1.79 | 12.06 | 99% | 0.37 | 1.55 | 2.66 | 148.4 → 149.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.75 | 10.60 | 99% | 0.36 | 1.52 | 2.69 | 123.1 → 121.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 3.04 | 11.27 | 99% | 2.32 | 2.39 | 2.99 | 149.2 → 145.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.40 | 1.84 | 18.42 | 99% | 0.41 | 1.61 | 2.89 | 150.2 → 146.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.39 | 1.76 | 11.83 | 99% | 0.38 | 1.55 | 2.74 | 153.0 → 149.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.34 | 1.42 | 8.10 | 100% | 0.28 | 1.21 | 2.39 | 112.6 → 110.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.41 | 1.42 | 11.35 | 99% | 0.38 | 1.14 | 2.90 | 152.4 → 149.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 1.26 | 11.48 | 99% | 0.38 | 1.09 | 2.35 | 152.2 → 149.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.11 | 9.76 | 100% | 0.26 | 0.95 | 2.10 | 73.7 → 73.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.176 |
| ArrangingLayoutable LineDrawing | 0.042 |
| ArrangingLayoutable CachedLineNumberMargin | 0.027 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.57 | 3.15 | 16.19 | 99% | 2.28 | 2.48 | 3.08 | 149.1 → 150.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.16 | 2.95 | 18.96 | 96% | 0.41 | 2.68 | 2.94 | 555.5 → 573.6 | 13 | 3.5 / 31 |

