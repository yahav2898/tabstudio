# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 2.13 | 12.05 | 100% | 0.29 | 1.91 | 2.58 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.51 | 2.36 | 14.33 | 99% | 0.45 | 2.01 | 2.75 | 122.1 → 122.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 3.84 | 16.50 | 99% | 2.49 | 2.35 | 3.04 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 2.87 | 101.06 | 99% | 0.55 | 2.20 | 3.18 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 2.20 | 12.54 | 99% | 0.39 | 1.87 | 2.49 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.40 | 2.16 | 9.71 | 100% | 0.37 | 1.89 | 2.53 | 105.0 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.36 | 2.08 | 14.46 | 99% | 0.33 | 1.84 | 2.42 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.44 | 2.19 | 11.96 | 99% | 0.39 | 1.91 | 2.52 | 104.3 → 96.0 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.32 | 2.01 | 11.27 | 99% | 0.29 | 1.80 | 2.31 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.39 | 2.19 | 14.35 | 99% | 0.39 | 1.93 | 2.61 | 107.4 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.49 | 3.04 | 15.62 | 99% | 0.47 | 2.29 | 2.38 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.32 | 11.29 | 100% | 0.85 | 1.86 | 2.05 | 46.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.200 |
| MeasuringLayoutable Panel | 0.047 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 2.42 | 2.44 | 100% | 0.40 | 1.91 | 2.91 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 26 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.42 | 2.14 | 21.85 | 99% | 0.36 | 1.90 | 2.55 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.91 | 2.75 | 23.67 | 98% | 0.63 | 2.26 | 3.25 | 308.9 → 301.0 | 7 | 1.1 / 4 |

