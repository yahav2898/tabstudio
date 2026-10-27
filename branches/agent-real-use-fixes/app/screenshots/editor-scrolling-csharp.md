# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.07 | 3.79 | 17.59 | 99% | 0.67 | 2.69 | 5.67 | 125.9 → 125.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 3.04 | 15.02 | 99% | 0.76 | 2.36 | 5.33 | 163.4 → 161.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 3.28 | 15.61 | 99% | 0.93 | 2.42 | 5.47 | 200.2 → 183.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 2.86 | 13.89 | 99% | 0.69 | 2.25 | 5.16 | 143.0 → 139.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 3.54 | 14.27 | 99% | 0.85 | 2.53 | 5.21 | 119.3 → 112.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.11 | 3.06 | 13.51 | 99% | 0.72 | 2.36 | 5.27 | 144.6 → 137.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.02 | 2.88 | 14.93 | 99% | 0.66 | 2.30 | 5.05 | 144.8 → 136.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 2.90 | 14.16 | 99% | 0.67 | 2.25 | 4.93 | 143.7 → 135.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.06 | 3.38 | 12.35 | 99% | 0.70 | 2.58 | 5.01 | 121.4 → 117.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.14 | 3.10 | 12.57 | 99% | 0.72 | 2.34 | 5.41 | 147.9 → 139.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.10 | 2.98 | 14.85 | 99% | 0.73 | 2.36 | 4.73 | 147.0 → 139.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 2.42 | 13.49 | 99% | 0.63 | 1.90 | 3.93 | 82.4 → 79.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.299 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 2.78 | 3.03 | 100% | 0.52 | 2.10 | 5.44 | 88.5 → 110.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 192 frames at 120 Hz; the text stood still on 0 of them, 225 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 2.87 | 16.31 | 99% | 0.71 | 2.27 | 5.18 | 142.5 → 140.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.44 | 3.25 | 18.08 | 98% | 0.76 | 2.55 | 5.03 | 357.9 → 350.4 | 8 | 1.1 / 4 |

