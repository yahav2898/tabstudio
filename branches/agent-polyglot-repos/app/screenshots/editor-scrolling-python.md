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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 1.91 | 2.37 | 100% | 0.45 | 1.61 | 4.66 | 128.8 → 136.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.27 | 47.35 | 100% | 0.69 | 1.68 | 5.12 | 165.3 → 162.9 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 2.51 | 4.09 | 100% | 0.91 | 1.72 | 5.75 | 212.1 → 197.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.28 | 4.39 | 100% | 0.66 | 1.73 | 5.39 | 150.5 → 152.0 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.92 | 50.92 | 100% | 0.81 | 2.36 | 5.52 | 126.1 → 123.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.10 | 45.60 | 100% | 0.68 | 1.65 | 5.33 | 149.9 → 146.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.09 | 2.68 | 100% | 0.65 | 1.64 | 5.31 | 152.1 → 148.8 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 2.22 | 50.07 | 100% | 0.68 | 1.69 | 5.65 | 155.1 → 151.9 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.09 | 44.25 | 100% | 0.53 | 1.61 | 5.32 | 114.8 → 113.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.82 | 4.52 | 100% | 0.91 | 2.52 | 5.63 | 155.3 → 152.0 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.09 | 35.78 | 100% | 0.64 | 1.62 | 4.79 | 154.8 → 151.7 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 1.65 | 2.16 | 100% | 0.47 | 1.30 | 4.38 | 75.7 → 75.2 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 2.13 | 46.49 | 100% | 0.67 | 1.66 | 5.47 | 150.8 → 152.1 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.22 | 7.56 | 75.64 | 96% | 2.38 | 6.64 | 6.43 | 566.4 → 574.4 | 4 | 3.5 / 31 |

