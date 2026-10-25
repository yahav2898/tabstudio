# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
- 0 classified spans, 0 diagnostics, 683 folds, 0 breakpoints, 0 Live Unit Testing markers.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 2.34 | 7.91 | 100% | 0.54 | 1.78 | 4.76 | 131.2 → 138.1 | 6 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 2.48 | 13.16 | 99% | 0.72 | 1.75 | 4.98 | 161.2 → 159.0 | 8 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 2.86 | 12.88 | 99% | 1.03 | 1.87 | 5.39 | 200.7 → 188.1 | 7 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.46 | 8.43 | 100% | 0.73 | 1.71 | 4.95 | 149.1 → 148.0 | 7 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 1.97 | 6.33 | 100% | 0.67 | 1.42 | 4.72 | 81.1 → 77.9 | 5 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.36 | 11.65 | 99% | 0.70 | 1.69 | 4.90 | 148.3 → 142.4 | 6 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.48 | 10.68 | 99% | 0.73 | 1.76 | 5.06 | 150.6 → 145.2 | 8 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 2.74 | 10.88 | 99% | 0.69 | 1.85 | 4.96 | 153.4 → 148.0 | 6 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 2.25 | 12.25 | 99% | 0.55 | 1.65 | 4.64 | 112.7 → 108.8 | 6 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.33 | 7.94 | 100% | 0.65 | 1.71 | 4.93 | 153.4 → 148.0 | 7 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.42 | 14.53 | 99% | 0.65 | 1.70 | 4.41 | 153.3 → 147.8 | 7 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.67 | 8.22 | 100% | 0.49 | 1.28 | 4.00 | 32.2 → 29.9 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.306 |
| MeasuringLayoutable Border | 0.033 |
| ArrangingLayoutable TextView | 0.021 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.44 | 7.19 | 100% | 0.62 | 1.70 | 4.88 | 150.4 → 149.3 | 7 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.12 | 11.05 | 20.31 | 93% | 0.87 | 8.63 | 9.06 | 598.1 → 618.2 | 20 | 3.5 / 31 |

