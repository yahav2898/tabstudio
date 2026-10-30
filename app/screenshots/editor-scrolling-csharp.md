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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 3.51 | 12.06 | 100% | 0.50 | 3.10 | 4.71 | 87.1 → 88.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 4.01 | 19.50 | 99% | 0.82 | 3.25 | 4.95 | 125.4 → 123.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.35 | 6.06 | 18.82 | 98% | 2.87 | 3.38 | 6.01 | 161.7 → 145.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 3.82 | 20.95 | 99% | 0.74 | 3.18 | 5.01 | 104.9 → 101.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.44 | 4.89 | 100% | 0.63 | 2.87 | 4.59 | 81.9 → 74.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 3.54 | 20.07 | 99% | 0.65 | 2.99 | 4.73 | 107.5 → 99.0 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.98 | 3.89 | 19.36 | 99% | 0.88 | 3.17 | 4.97 | 107.5 → 99.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 3.59 | 16.11 | 99% | 0.65 | 2.99 | 4.64 | 106.8 → 98.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.30 | 14.33 | 100% | 0.52 | 2.88 | 4.33 | 84.3 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 3.49 | 18.28 | 100% | 0.64 | 2.95 | 4.68 | 110.1 → 101.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 3.57 | 18.70 | 99% | 0.66 | 2.98 | 4.14 | 110.2 → 101.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.48 | 12.78 | 100% | 1.94 | 2.78 | 3.72 | 46.5 → 42.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.253 |
| AttachingStyle - | 0.070 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 3.42 | 3.48 | 100% | 0.49 | 2.86 | 4.66 | 50.4 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 210 frames at 120 Hz; the text stood still on 0 of them, 242 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 3.59 | 22.30 | 99% | 0.62 | 2.97 | 4.77 | 105.8 → 102.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.54 | 4.47 | 25.15 | 98% | 1.08 | 3.56 | 5.16 | 320.6 → 312.4 | 8 | 1.1 / 4 |

