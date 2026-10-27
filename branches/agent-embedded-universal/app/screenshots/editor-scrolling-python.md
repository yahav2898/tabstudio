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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 3.26 | 12.20 | 99% | 0.54 | 2.62 | 4.54 | 129.3 → 136.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 2.86 | 15.93 | 99% | 0.63 | 2.38 | 4.33 | 165.3 → 162.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.03 | 15.82 | 99% | 0.82 | 2.45 | 4.51 | 212.1 → 197.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.77 | 15.77 | 99% | 0.60 | 2.37 | 4.05 | 150.6 → 151.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 2.90 | 12.87 | 99% | 0.67 | 2.39 | 4.29 | 125.7 → 123.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 3.41 | 88.83 | 99% | 0.68 | 2.63 | 4.44 | 150.4 → 146.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.77 | 17.55 | 99% | 0.65 | 2.29 | 4.56 | 152.6 → 148.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 2.49 | 14.74 | 99% | 0.62 | 1.98 | 4.29 | 155.1 → 151.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.58 | 11.66 | 99% | 0.49 | 2.23 | 4.03 | 114.8 → 112.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 3.19 | 18.61 | 99% | 0.69 | 2.54 | 4.24 | 154.8 → 152.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.91 | 14.44 | 99% | 0.68 | 2.31 | 3.94 | 155.0 → 151.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.39 | 10.83 | 100% | 0.46 | 2.10 | 3.41 | 75.7 → 75.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.238 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.94 | 17.28 | 99% | 0.64 | 2.44 | 4.34 | 150.7 → 151.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.71 | 7.44 | 28.11 | 95% | 0.90 | 6.69 | 4.78 | 558.0 → 578.7 | 14 | 3.5 / 31 |

