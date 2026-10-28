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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 3.30 | 15.63 | 100% | 0.48 | 2.84 | 4.70 | 86.5 → 87.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.83 | 16.56 | 100% | 1.61 | 3.05 | 5.11 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 3.60 | 18.93 | 99% | 0.94 | 2.90 | 4.93 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.31 | 16.37 | 99% | 0.63 | 2.79 | 4.66 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 3.06 | 14.40 | 100% | 0.64 | 2.59 | 4.45 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 3.46 | 18.26 | 99% | 0.75 | 2.83 | 4.89 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.90 | 3.44 | 90.02 | 99% | 0.71 | 2.82 | 4.85 | 106.6 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.34 | 18.07 | 99% | 0.65 | 2.83 | 4.64 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.24 | 14.60 | 99% | 0.56 | 2.79 | 4.54 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 3.60 | 16.97 | 99% | 0.74 | 2.93 | 4.92 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.45 | 17.36 | 99% | 0.70 | 2.93 | 4.24 | 109.3 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.93 | 12.97 | 100% | 1.15 | 2.52 | 3.35 | 46.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.288 |
| ArrangingLayoutable ScrollingTextLayer | 0.062 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.75 | 3.46 | 3.55 | 100% | 0.51 | 2.86 | 4.93 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 208 frames at 120 Hz; the text stood still on 0 of them, 241 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.06 | 3.89 | 16.08 | 99% | 1.65 | 3.16 | 5.28 | 104.9 → 101.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 3.91 | 19.15 | 98% | 0.78 | 3.25 | 4.72 | 318.4 → 310.4 | 7 | 1.1 / 4 |

