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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.41 | 14.23 | 99% | 0.47 | 2.94 | 4.38 | 86.5 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 3.77 | 17.22 | 99% | 0.83 | 3.07 | 4.64 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 4.16 | 108.29 | 99% | 1.88 | 3.22 | 5.08 | 160.0 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.56 | 10.84 | 100% | 0.64 | 2.99 | 4.54 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 3.41 | 18.50 | 99% | 0.65 | 2.89 | 4.37 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 3.52 | 19.05 | 99% | 0.63 | 2.99 | 4.61 | 106.5 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 4.95 | 21.62 | 99% | 1.78 | 3.22 | 4.93 | 106.5 → 98.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.52 | 13.06 | 99% | 0.63 | 2.97 | 4.55 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.34 | 15.15 | 100% | 0.55 | 2.88 | 4.30 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 3.40 | 16.30 | 99% | 0.62 | 2.94 | 4.54 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 3.42 | 18.36 | 99% | 0.62 | 2.93 | 4.04 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 3.32 | 16.12 | 100% | 1.63 | 2.74 | 3.56 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.239 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 3.38 | 3.58 | 100% | 0.56 | 2.77 | 4.74 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 213 frames at 120 Hz; the text stood still on 0 of them, 246 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.60 | 15.52 | 99% | 0.63 | 3.07 | 4.71 | 104.9 → 101.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.37 | 5.97 | 21.84 | 98% | 0.86 | 3.63 | 4.88 | 318.5 → 310.4 | 9 | 1.1 / 4 |

