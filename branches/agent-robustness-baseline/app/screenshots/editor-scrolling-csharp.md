# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.73 | 4.71 | 100% | 0.34 | 2.44 | 4.15 | 86.4 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.02 | 14.97 | 99% | 0.62 | 2.55 | 4.17 | 124.2 → 121.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 3.14 | 16.83 | 99% | 0.78 | 2.64 | 4.32 | 159.9 → 144.2 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 3.83 | 11.52 | 99% | 0.87 | 2.78 | 4.43 | 103.9 → 101.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.69 | 14.25 | 100% | 0.52 | 2.35 | 3.92 | 81.3 → 73.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 3.00 | 16.09 | 99% | 0.54 | 2.60 | 4.56 | 106.6 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 3.01 | 12.46 | 99% | 0.52 | 2.59 | 4.37 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.65 | 14.04 | 99% | 1.45 | 2.70 | 4.36 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.53 | 2.84 | 9.14 | 100% | 0.41 | 2.49 | 3.91 | 83.8 → 78.3 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 2.94 | 14.40 | 99% | 0.52 | 2.56 | 4.10 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 2.91 | 11.78 | 99% | 0.54 | 2.55 | 3.61 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.72 | 11.23 | 100% | 1.03 | 2.38 | 3.09 | 46.5 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.253 |
| AttachingStyle - | 0.044 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable LineDrawing | 0.019 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.47 | 2.78 | 3.21 | 100% | 0.33 | 2.38 | 4.19 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 254 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 2.98 | 14.78 | 99% | 0.51 | 2.57 | 4.14 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 5.53 | 19.06 | 98% | 1.39 | 3.20 | 4.44 | 318.4 → 310.4 | 8 | 1.1 / 4 |

