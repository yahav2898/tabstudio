# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.03 | 16.35 | 100% | 0.49 | 2.64 | 4.50 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.52 | 19.22 | 99% | 0.95 | 2.87 | 5.10 | 129.3 → 129.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.22 | 4.54 | 20.87 | 99% | 2.60 | 3.01 | 6.09 | 176.1 → 162.2 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.13 | 3.84 | 97.91 | 99% | 0.89 | 3.08 | 5.90 | 114.9 → 115.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.95 | 3.65 | 15.39 | 100% | 0.92 | 2.87 | 5.85 | 91.2 → 89.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.95 | 3.79 | 20.13 | 99% | 0.91 | 3.05 | 5.59 | 115.1 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.92 | 3.59 | 21.35 | 99% | 1.65 | 2.96 | 5.79 | 117.1 → 113.0 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.53 | 18.46 | 99% | 1.54 | 2.93 | 5.33 | 119.9 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.67 | 3.24 | 13.58 | 99% | 0.70 | 2.77 | 5.32 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.83 | 3.55 | 18.11 | 99% | 0.85 | 2.92 | 5.75 | 119.6 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 3.36 | 17.10 | 99% | 1.04 | 2.80 | 4.79 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.91 | 17.01 | 99% | 1.48 | 2.55 | 4.49 | 43.6 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.343 |
| AttachingStyle - | 0.062 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 3.21 | 27.97 | 99% | 0.71 | 2.66 | 4.52 | 116.0 → 117.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.50 | 10.92 | 31.84 | 91% | 2.00 | 9.31 | 9.56 | 598.8 → 625.2 | 15 | 3.5 / 30 |

