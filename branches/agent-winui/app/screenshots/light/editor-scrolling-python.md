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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 2.32 | 78.89 | 99% | 0.37 | 1.91 | 2.95 | 118.7 → 125.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.40 | 1.70 | 10.20 | 100% | 0.34 | 1.50 | 2.61 | 154.1 → 151.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 1.54 | 11.28 | 99% | 0.46 | 1.13 | 2.70 | 200.4 → 185.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.34 | 1.30 | 9.83 | 99% | 0.35 | 1.07 | 2.50 | 139.3 → 140.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.29 | 1.17 | 7.72 | 100% | 0.33 | 0.99 | 2.54 | 114.1 → 112.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.41 | 2.66 | 11.78 | 99% | 0.46 | 2.25 | 2.57 | 139.0 → 135.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.42 | 1.78 | 12.65 | 99% | 0.39 | 1.37 | 2.71 | 141.5 → 137.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.35 | 10.22 | 99% | 0.34 | 1.09 | 2.65 | 144.2 → 140.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.31 | 1.47 | 5.85 | 100% | 0.25 | 1.21 | 2.24 | 103.7 → 102.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.58 | 10.07 | 99% | 0.34 | 1.44 | 2.49 | 143.7 → 140.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 1.25 | 10.19 | 99% | 0.34 | 1.06 | 2.24 | 143.4 → 140.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.38 | 3.24 | 100% | 0.33 | 0.99 | 2.10 | 65.3 → 64.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| AttachingStyle - | 0.046 |
| ArrangingLayoutable CachedLineNumberMargin | 0.027 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| ArrangingLayoutable Track | 0.015 |
| ArrangingLayoutable NumberDrawing | 0.014 |
| ArrangingLayoutable TextView | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.35 | 1.30 | 11.52 | 99% | 0.35 | 1.09 | 2.70 | 139.8 → 140.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 2.78 | 16.81 | 96% | 0.40 | 2.26 | 2.78 | 536.9 → 555.3 | 12 | 3.5 / 31 |

