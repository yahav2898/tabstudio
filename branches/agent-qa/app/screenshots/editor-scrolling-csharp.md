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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 3.03 | 15.92 | 99% | 0.42 | 2.68 | 4.35 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.33 | 16.45 | 99% | 0.74 | 2.77 | 4.57 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 3.87 | 17.97 | 99% | 1.55 | 2.92 | 5.01 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.32 | 13.42 | 99% | 0.62 | 2.79 | 4.57 | 103.9 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 3.18 | 10.12 | 100% | 0.62 | 2.66 | 4.46 | 81.3 → 73.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.41 | 15.37 | 99% | 0.69 | 2.85 | 4.65 | 106.5 → 98.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 3.36 | 86.44 | 99% | 0.68 | 2.81 | 4.68 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.20 | 16.30 | 99% | 0.59 | 2.72 | 4.36 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.16 | 12.55 | 99% | 0.54 | 2.72 | 4.28 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 3.47 | 16.39 | 99% | 0.69 | 2.85 | 4.68 | 109.1 → 101.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 3.60 | 16.06 | 99% | 0.70 | 2.93 | 4.30 | 109.3 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.91 | 11.26 | 100% | 0.64 | 2.50 | 3.42 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| AttachingStyle - | 0.064 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 3.13 | 3.52 | 100% | 0.43 | 2.68 | 4.70 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 228 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 3.65 | 16.18 | 99% | 0.90 | 2.94 | 4.87 | 104.9 → 101.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.25 | 3.78 | 16.23 | 98% | 0.71 | 3.17 | 4.62 | 318.4 → 310.4 | 7 | 1.1 / 4 |

