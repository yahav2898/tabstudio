# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 3.35 | 14.21 | 100% | 0.54 | 2.95 | 4.56 | 86.7 → 87.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.01 | 3.77 | 19.09 | 99% | 0.95 | 2.99 | 4.63 | 124.6 → 122.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 5.37 | 18.64 | 99% | 2.51 | 3.18 | 5.29 | 160.3 → 144.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 3.60 | 20.08 | 99% | 0.66 | 2.95 | 4.61 | 104.1 → 101.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.80 | 3.41 | 16.89 | 99% | 0.72 | 2.75 | 4.32 | 81.1 → 73.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 3.62 | 19.95 | 99% | 0.73 | 2.99 | 4.66 | 106.8 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 4.23 | 17.53 | 99% | 0.89 | 3.17 | 5.05 | 106.7 → 98.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.93 | 3.69 | 19.10 | 99% | 0.70 | 3.03 | 4.64 | 106.0 → 97.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 3.39 | 12.64 | 99% | 0.60 | 2.88 | 4.37 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 3.42 | 17.80 | 99% | 0.66 | 2.84 | 4.37 | 109.3 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.41 | 18.23 | 99% | 0.64 | 2.89 | 3.99 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.24 | 20.59 | 99% | 1.67 | 2.68 | 3.52 | 46.1 → 42.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 3.46 | 3.55 | 100% | 0.52 | 2.85 | 4.48 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 210 frames at 120 Hz; the text stood still on 0 of them, 242 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.65 | 18.59 | 99% | 0.70 | 2.97 | 4.73 | 105.1 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 5.68 | 23.20 | 98% | 0.79 | 3.38 | 4.49 | 318.8 → 310.7 | 8 | 1.1 / 4 |

