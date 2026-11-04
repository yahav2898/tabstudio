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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.67 | 12.61 | 100% | 0.44 | 2.33 | 3.68 | 93.2 → 100.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.66 | 2.83 | 16.04 | 99% | 0.81 | 2.36 | 4.14 | 129.2 → 129.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 3.47 | 17.54 | 99% | 1.64 | 2.50 | 4.61 | 176.1 → 162.1 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 4.70 | 79.01 | 99% | 1.71 | 2.59 | 4.50 | 114.8 → 115.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.71 | 14.35 | 99% | 0.66 | 2.30 | 4.34 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.64 | 2.85 | 19.69 | 99% | 0.76 | 2.39 | 4.66 | 115.0 → 110.4 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.63 | 2.85 | 16.74 | 99% | 1.43 | 2.34 | 3.97 | 117.0 → 113.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.83 | 4.27 | 16.95 | 99% | 2.39 | 2.55 | 4.54 | 119.8 → 115.8 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.44 | 2.43 | 9.08 | 100% | 0.39 | 2.12 | 3.70 | 80.3 → 77.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.55 | 2.68 | 17.32 | 99% | 0.57 | 2.26 | 4.23 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.68 | 17.01 | 99% | 0.59 | 2.25 | 3.34 | 119.6 → 115.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.35 | 3.57 | 100% | 0.42 | 2.01 | 3.39 | 43.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.288 |
| ArrangingLayoutable NumberDrawing | 0.076 |
| AttachingStyle - | 0.036 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.52 | 2.58 | 36.12 | 99% | 0.52 | 2.20 | 4.15 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.82 | 5.77 | 25.18 | 95% | 1.48 | 4.65 | 7.24 | 574.7 → 603.7 | 13 | 3.5 / 30 |

