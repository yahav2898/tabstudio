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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.07 | 2.94 | 21.69 | 99% | 0.62 | 2.41 | 6.11 | 133.5 → 140.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.38 | 3.64 | 21.42 | 99% | 0.94 | 2.80 | 7.18 | 169.6 → 167.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.45 | 3.51 | 22.42 | 99% | 1.11 | 2.54 | 7.29 | 215.7 → 201.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.22 | 3.13 | 22.87 | 99% | 0.82 | 2.44 | 6.96 | 155.0 → 156.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 1.07 | 3.26 | 24.62 | 100% | 0.84 | 2.40 | 6.96 | 129.6 → 128.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.25 | 3.19 | 21.19 | 100% | 0.87 | 2.50 | 7.34 | 154.6 → 150.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.19 | 3.13 | 21.77 | 99% | 0.83 | 2.40 | 6.96 | 156.6 → 153.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.24 | 3.13 | 22.20 | 99% | 0.81 | 2.45 | 6.36 | 159.3 → 156.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.08 | 3.40 | 20.57 | 99% | 0.69 | 2.57 | 6.53 | 119.3 → 117.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.24 | 3.08 | 41.22 | 99% | 0.83 | 2.38 | 6.91 | 159.0 → 156.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.16 | 3.06 | 21.16 | 99% | 0.81 | 2.35 | 5.67 | 158.9 → 155.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.78 | 2.76 | 12.23 | 100% | 0.64 | 2.15 | 5.58 | 78.8 → 79.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.320 |
| ArrangingLayoutable LineDrawing | 0.071 |
| AttachingStyle - | 0.037 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.24 | 3.19 | 22.49 | 99% | 0.84 | 2.47 | 6.51 | 155.2 → 156.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.88 | 8.10 | 34.71 | 95% | 1.08 | 7.32 | 7.85 | 556.2 → 582.9 | 10 | 3.5 / 31 |

