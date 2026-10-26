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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.03 | 3.36 | 15.41 | 99% | 0.60 | 2.81 | 5.29 | 128.0 → 135.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.28 | 4.53 | 19.00 | 99% | 0.94 | 3.21 | 5.53 | 164.2 → 161.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.37 | 3.78 | 17.52 | 99% | 1.06 | 2.99 | 5.60 | 210.5 → 195.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 3.50 | 15.83 | 99% | 0.77 | 2.82 | 5.44 | 149.1 → 150.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.91 | 3.22 | 13.00 | 99% | 0.75 | 2.65 | 5.35 | 123.7 → 121.8 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.86 | 84.82 | 99% | 0.91 | 3.01 | 5.74 | 149.3 → 144.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 3.57 | 19.86 | 99% | 0.84 | 2.85 | 5.85 | 151.1 → 148.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 3.67 | 18.10 | 99% | 0.81 | 2.97 | 5.36 | 154.0 → 150.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.99 | 3.46 | 12.56 | 99% | 0.64 | 2.87 | 5.11 | 112.6 → 111.6 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 3.76 | 15.45 | 99% | 0.84 | 3.02 | 5.40 | 154.2 → 150.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 3.46 | 15.45 | 99% | 0.77 | 2.85 | 4.62 | 152.6 → 149.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.68 | 3.02 | 8.11 | 100% | 0.57 | 2.57 | 4.33 | 74.3 → 73.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.303 |
| ArrangingLayoutable CachedLineNumberMargin | 0.034 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.15 | 3.78 | 14.73 | 99% | 0.81 | 2.95 | 5.60 | 150.5 → 150.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.52 | 6.74 | 31.71 | 96% | 0.92 | 5.87 | 5.65 | 548.0 → 568.0 | 13 | 3.5 / 31 |

