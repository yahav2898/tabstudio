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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 2.80 | 17.98 | 99% | 0.45 | 2.28 | 3.22 | 84.9 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.48 | 2.33 | 15.16 | 99% | 0.43 | 2.00 | 2.71 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.53 | 2.51 | 15.60 | 99% | 0.58 | 2.06 | 2.78 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.44 | 2.21 | 14.23 | 99% | 0.39 | 1.92 | 2.60 | 102.3 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.37 | 14.96 | 99% | 0.52 | 2.02 | 2.84 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.47 | 2.33 | 9.96 | 100% | 0.44 | 1.99 | 2.75 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.61 | 2.54 | 16.99 | 99% | 0.48 | 2.12 | 2.93 | 104.9 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.87 | 13.26 | 100% | 1.40 | 2.39 | 3.57 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.55 | 2.74 | 14.50 | 99% | 0.44 | 2.34 | 3.02 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.49 | 2.30 | 10.16 | 100% | 0.41 | 1.98 | 2.78 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.40 | 2.14 | 14.94 | 99% | 0.35 | 1.90 | 2.24 | 107.6 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.90 | 7.95 | 100% | 0.58 | 1.71 | 1.66 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.160 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.45 | 2.27 | 2.58 | 100% | 0.38 | 1.94 | 2.67 | 49.3 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 27 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.14 | 26.28 | 99% | 1.43 | 2.35 | 3.32 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.78 | 2.61 | 16.33 | 98% | 0.54 | 2.15 | 2.91 | 308.9 → 301.1 | 7 | 1.1 / 4 |

