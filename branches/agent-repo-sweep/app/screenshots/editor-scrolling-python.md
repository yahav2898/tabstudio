# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.87 | 12.78 | 100% | 0.44 | 2.57 | 4.27 | 96.3 → 103.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.36 | 18.36 | 99% | 1.32 | 2.73 | 5.05 | 133.0 → 130.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 4.99 | 20.36 | 99% | 2.45 | 2.89 | 5.41 | 180.7 → 165.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 3.25 | 17.30 | 99% | 0.69 | 2.67 | 5.04 | 118.0 → 119.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.95 | 15.11 | 99% | 0.67 | 2.49 | 5.02 | 93.0 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.66 | 3.15 | 17.92 | 99% | 0.73 | 2.62 | 5.11 | 118.0 → 113.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 4.69 | 19.42 | 99% | 1.71 | 2.81 | 5.28 | 120.1 → 116.6 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.12 | 15.72 | 99% | 0.71 | 2.67 | 5.00 | 123.0 → 119.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.98 | 12.63 | 99% | 0.54 | 2.60 | 4.72 | 82.2 → 80.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 3.20 | 15.29 | 99% | 0.75 | 2.68 | 4.88 | 122.8 → 119.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 4.34 | 86.48 | 99% | 1.62 | 2.80 | 4.76 | 122.9 → 119.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.64 | 7.03 | 100% | 0.46 | 2.34 | 4.04 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.313 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.12 | 15.33 | 99% | 0.70 | 2.66 | 4.88 | 118.7 → 119.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.37 | 14.05 | 32.42 | 92% | 2.13 | 11.24 | 9.13 | 621.3 → 650.2 | 15 | 3.5 / 31 |

