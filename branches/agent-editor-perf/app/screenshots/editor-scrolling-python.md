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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 2.49 | 13.86 | 99% | 0.61 | 1.92 | 5.22 | 129.1 → 136.0 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.01 | 2.77 | 15.07 | 99% | 0.82 | 1.92 | 5.31 | 158.8 → 156.5 | 7 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 4.05 | 14.87 | 98% | 1.20 | 2.21 | 5.77 | 197.7 → 185.2 | 7 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.93 | 2.59 | 12.79 | 99% | 0.75 | 1.78 | 5.24 | 146.8 → 145.7 | 6 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.45 | 7.39 | 100% | 0.82 | 1.60 | 5.37 | 80.4 → 77.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.93 | 2.54 | 14.02 | 99% | 0.77 | 1.79 | 5.40 | 146.0 → 140.1 | 6 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.96 | 2.97 | 15.53 | 99% | 0.86 | 1.87 | 5.55 | 148.3 → 142.9 | 6 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.02 | 2.96 | 17.11 | 99% | 0.80 | 1.94 | 5.44 | 151.1 → 145.7 | 6 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 2.24 | 12.81 | 99% | 0.57 | 1.70 | 5.06 | 111.0 → 107.1 | 5 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 3.18 | 18.41 | 99% | 0.87 | 2.02 | 5.65 | 151.1 → 145.7 | 6 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.98 | 2.71 | 16.37 | 99% | 0.78 | 1.96 | 5.11 | 150.9 → 145.6 | 7 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 1.86 | 2.63 | 100% | 0.56 | 1.35 | 4.35 | 32.2 → 29.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| ArrangingLayoutable Track | 0.033 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Panel | 0.001 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.00 | 2.74 | 15.25 | 99% | 0.80 | 1.90 | 5.36 | 148.0 → 147.0 | 6 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.76 | 11.88 | 27.49 | 93% | 1.04 | 10.77 | 10.05 | 599.5 → 622.3 | 19 | 3.5 / 31 |

