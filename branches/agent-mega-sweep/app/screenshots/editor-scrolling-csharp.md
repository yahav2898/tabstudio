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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 3.11 | 32.05 | 99% | 0.49 | 2.63 | 4.47 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 3.34 | 19.03 | 100% | 0.69 | 2.76 | 4.64 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.59 | 17.74 | 99% | 0.94 | 2.81 | 4.80 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.85 | 3.45 | 17.88 | 99% | 0.69 | 2.85 | 4.80 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 3.54 | 16.71 | 99% | 1.75 | 2.77 | 4.87 | 80.5 → 73.7 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.83 | 3.43 | 18.65 | 99% | 0.69 | 2.79 | 4.73 | 105.1 → 96.7 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.79 | 3.30 | 15.00 | 100% | 0.65 | 2.78 | 4.78 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.27 | 17.25 | 99% | 0.68 | 2.75 | 4.54 | 104.2 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.78 | 3.28 | 13.61 | 99% | 0.66 | 2.80 | 4.60 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.81 | 3.41 | 17.80 | 99% | 0.67 | 2.77 | 4.68 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 3.28 | 16.22 | 99% | 0.63 | 2.72 | 4.08 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 2.95 | 4.24 | 100% | 0.70 | 2.48 | 3.51 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.301 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable TextView | 0.018 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 3.45 | 5.74 | 100% | 0.70 | 2.81 | 5.24 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 175 frames at 120 Hz; the text stood still on 0 of them, 210 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.40 | 16.54 | 100% | 0.64 | 2.87 | 4.83 | 103.3 → 99.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 4.07 | 22.10 | 98% | 0.88 | 3.30 | 4.99 | 308.9 → 301.1 | 7 | 1.1 / 4 |

