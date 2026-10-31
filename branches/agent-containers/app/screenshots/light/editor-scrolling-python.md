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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 2.34 | 17.20 | 99% | 0.45 | 1.99 | 2.94 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.64 | 2.45 | 15.71 | 99% | 1.30 | 1.97 | 3.37 | 129.3 → 129.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.73 | 2.62 | 16.34 | 99% | 1.50 | 2.02 | 3.56 | 176.1 → 162.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 2.33 | 15.52 | 99% | 0.51 | 1.95 | 3.33 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 2.36 | 15.03 | 99% | 1.13 | 1.93 | 3.39 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.58 | 2.52 | 20.52 | 99% | 0.62 | 2.05 | 3.40 | 115.0 → 110.4 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.65 | 3.70 | 17.94 | 99% | 1.48 | 2.35 | 3.30 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.83 | 15.44 | 99% | 1.38 | 2.11 | 3.28 | 119.9 → 115.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.40 | 2.12 | 10.69 | 99% | 0.47 | 1.85 | 2.79 | 80.3 → 78.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.63 | 2.60 | 16.22 | 99% | 0.94 | 2.09 | 3.77 | 119.5 → 115.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.60 | 2.41 | 18.58 | 99% | 1.15 | 2.02 | 2.95 | 119.7 → 115.7 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.10 | 11.48 | 100% | 0.45 | 1.77 | 2.48 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.225 |
| AttachingStyle - | 0.073 |
| FindingResource - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 4.23 | 99.27 | 99% | 1.67 | 2.41 | 3.83 | 116.0 → 117.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.85 | 4.91 | 26.20 | 96% | 1.10 | 4.03 | 5.91 | 569.8 → 596.3 | 13 | 3.5 / 30 |

