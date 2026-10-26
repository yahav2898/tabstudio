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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.03 | 2.80 | 14.52 | 99% | 0.63 | 2.29 | 5.26 | 123.4 → 131.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.25 | 3.50 | 16.35 | 99% | 0.97 | 2.70 | 5.84 | 161.1 → 158.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.46 | 16.41 | 99% | 1.14 | 2.65 | 5.65 | 208.2 → 193.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.14 | 3.35 | 14.97 | 99% | 0.82 | 2.74 | 5.45 | 146.4 → 147.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.99 | 3.42 | 12.84 | 99% | 0.87 | 2.65 | 5.49 | 120.2 → 118.8 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.47 | 18.05 | 99% | 0.84 | 2.80 | 5.48 | 146.1 → 141.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.14 | 3.40 | 12.15 | 99% | 0.83 | 2.75 | 5.76 | 148.2 → 144.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.19 | 3.63 | 20.03 | 99% | 0.85 | 2.87 | 5.65 | 150.6 → 147.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.03 | 3.06 | 14.67 | 99% | 0.65 | 2.57 | 5.09 | 109.1 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.15 | 2.96 | 17.45 | 99% | 0.83 | 2.18 | 5.26 | 149.8 → 146.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.10 | 2.82 | 13.66 | 99% | 0.81 | 2.15 | 4.79 | 149.5 → 146.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.76 | 2.55 | 9.95 | 99% | 0.66 | 1.83 | 4.38 | 70.2 → 70.0 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.331 |
| AttachingStyle - | 0.069 |
| ArrangingLayoutable TextView | 0.035 |
| ArrangingLayoutable CachedLineNumberMargin | 0.023 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 2.82 | 12.61 | 99% | 0.82 | 2.16 | 5.18 | 145.6 → 146.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.86 | 7.38 | 30.48 | 95% | 0.98 | 6.35 | 5.66 | 552.3 → 577.7 | 13 | 3.5 / 31 |

