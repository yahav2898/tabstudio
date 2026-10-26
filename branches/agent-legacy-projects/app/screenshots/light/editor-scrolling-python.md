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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.20 | 12.58 | 99% | 0.46 | 1.80 | 4.44 | 128.7 → 136.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 2.42 | 14.82 | 99% | 0.68 | 1.89 | 4.75 | 164.8 → 162.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.33 | 5.21 | 17.08 | 98% | 2.87 | 2.68 | 5.73 | 212.3 → 197.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.40 | 15.66 | 99% | 0.65 | 1.95 | 4.84 | 150.3 → 151.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.26 | 12.47 | 99% | 0.63 | 1.78 | 4.87 | 125.0 → 123.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 2.33 | 14.89 | 99% | 0.63 | 1.82 | 4.79 | 149.5 → 145.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 4.33 | 15.52 | 99% | 2.55 | 2.58 | 5.33 | 152.6 → 149.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 2.52 | 13.48 | 99% | 0.68 | 1.98 | 4.88 | 154.5 → 151.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 2.20 | 10.54 | 99% | 0.54 | 1.81 | 4.81 | 114.5 → 112.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.29 | 13.54 | 99% | 0.61 | 1.81 | 4.78 | 154.2 → 151.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 4.54 | 18.26 | 99% | 0.90 | 3.98 | 5.01 | 154.8 → 152.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.79 | 10.35 | 99% | 0.41 | 1.48 | 3.91 | 75.4 → 74.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| MeasuringLayoutable Grid | 0.048 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 2.61 | 15.78 | 99% | 0.73 | 2.00 | 4.95 | 150.4 → 151.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.03 | 8.27 | 31.31 | 95% | 1.02 | 7.15 | 5.80 | 550.1 → 576.5 | 14 | 3.5 / 31 |

