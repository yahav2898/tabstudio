# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.54 | 55.17 | 100% | 0.49 | 2.10 | 4.58 | 95.5 → 102.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.68 | 3.93 | 100% | 0.87 | 2.09 | 4.73 | 131.7 → 129.4 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.76 | 51.58 | 100% | 1.69 | 2.33 | 5.88 | 178.7 → 164.0 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.01 | 4.60 | 100% | 1.30 | 2.28 | 5.52 | 116.9 → 118.3 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 2.40 | 31.92 | 100% | 0.76 | 1.92 | 5.38 | 92.3 → 90.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 2.45 | 3.23 | 100% | 0.68 | 2.00 | 5.35 | 116.8 → 112.7 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.76 | 48.77 | 100% | 0.75 | 2.20 | 5.56 | 118.9 → 115.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 4.65 | 9.94 | 100% | 2.41 | 4.01 | 5.89 | 121.9 → 118.4 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.48 | 31.53 | 100% | 0.52 | 2.11 | 5.27 | 81.7 → 79.6 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 2.90 | 6.31 | 100% | 0.84 | 2.33 | 5.70 | 121.6 → 118.3 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.54 | 45.49 | 100% | 0.74 | 2.08 | 4.74 | 121.7 → 118.1 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.19 | 3.34 | 100% | 0.59 | 1.84 | 4.56 | 43.8 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.369 |
| ArrangingLayoutable LineDrawing | 0.069 |
| AttachingStyle - | 0.068 |
| ArrangingLayoutable CachedLineNumberMargin | 0.031 |
| FindingResource - | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable Track | 0.017 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.75 | 3.64 | 100% | 0.70 | 2.19 | 4.98 | 117.6 → 118.3 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.72 | 5.52 | 65.16 | 99% | 0.85 | 4.70 | 9.48 | 619.0 → 638.9 | 2 | 3.5 / 31 |

