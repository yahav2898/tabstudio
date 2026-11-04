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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.37 | 2.10 | 10.53 | 99% | 0.38 | 1.86 | 3.28 | 94.3 → 101.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.28 | 14.82 | 99% | 0.57 | 1.92 | 2.92 | 131.1 → 131.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.49 | 2.36 | 14.81 | 99% | 1.10 | 1.88 | 3.10 | 178.8 → 164.6 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.40 | 2.04 | 13.50 | 99% | 0.43 | 1.83 | 2.61 | 116.3 → 117.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.99 | 14.15 | 99% | 0.40 | 1.75 | 2.82 | 92.3 → 89.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.37 | 2.05 | 14.47 | 99% | 0.39 | 1.78 | 2.84 | 116.6 → 111.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.48 | 2.78 | 18.85 | 99% | 1.41 | 2.01 | 2.86 | 118.6 → 114.5 | 4 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.51 | 2.76 | 13.06 | 99% | 1.52 | 2.11 | 3.15 | 121.4 → 117.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.33 | 1.99 | 10.54 | 99% | 0.44 | 1.78 | 2.78 | 81.2 → 78.8 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.44 | 2.41 | 16.93 | 99% | 0.60 | 1.90 | 3.06 | 121.1 → 117.3 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.37 | 2.04 | 14.30 | 99% | 0.40 | 1.81 | 2.42 | 121.2 → 117.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.80 | 10.31 | 100% | 0.29 | 1.65 | 2.25 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.150 |
| ArrangingLayoutable LineDrawing | 0.055 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.50 | 2.70 | 30.46 | 99% | 1.38 | 2.01 | 2.69 | 117.5 → 118.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.45 | 7.11 | 92.20 | 95% | 1.30 | 6.04 | 5.41 | 583.0 → 600.6 | 14 | 3.5 / 30 |

