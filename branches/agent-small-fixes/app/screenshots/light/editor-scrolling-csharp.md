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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 2.65 | 12.02 | 99% | 0.55 | 2.19 | 5.08 | 117.7 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.20 | 4.39 | 76.69 | 99% | 0.85 | 2.83 | 5.13 | 155.6 → 154.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.27 | 3.22 | 14.03 | 99% | 0.91 | 2.32 | 5.14 | 191.2 → 175.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.80 | 14.12 | 99% | 0.67 | 2.21 | 4.94 | 135.3 → 132.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 2.56 | 10.03 | 99% | 0.64 | 2.01 | 4.71 | 111.6 → 105.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.07 | 4.27 | 12.97 | 99% | 0.83 | 2.47 | 5.20 | 137.7 → 130.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.02 | 2.89 | 12.54 | 99% | 0.70 | 2.27 | 4.96 | 137.6 → 129.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.01 | 2.82 | 12.41 | 99% | 0.69 | 2.21 | 4.81 | 136.6 → 128.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.93 | 2.69 | 11.08 | 99% | 0.60 | 2.15 | 4.64 | 114.6 → 109.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 4.60 | 13.99 | 99% | 0.77 | 4.11 | 5.18 | 140.5 → 133.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 2.75 | 15.09 | 99% | 0.67 | 2.20 | 4.42 | 139.7 → 131.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.35 | 9.79 | 99% | 0.57 | 1.88 | 3.71 | 76.9 → 73.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.254 |
| AttachingStyle - | 0.051 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.88 | 2.73 | 2.91 | 100% | 0.51 | 2.15 | 5.42 | 82.7 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 238 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.07 | 2.98 | 13.05 | 99% | 0.70 | 2.33 | 5.04 | 136.6 → 133.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.47 | 3.39 | 17.57 | 98% | 0.78 | 2.58 | 4.90 | 348.2 → 341.5 | 8 | 1.1 / 4 |

