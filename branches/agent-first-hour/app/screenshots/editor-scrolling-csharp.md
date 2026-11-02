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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.16 | 50.28 | 100% | 0.41 | 1.87 | 4.34 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 2.46 | 3.72 | 100% | 0.64 | 1.94 | 4.48 | 122.1 → 122.5 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 2.50 | 55.56 | 100% | 0.90 | 1.93 | 4.57 | 158.1 → 143.1 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.39 | 3.47 | 100% | 0.59 | 1.92 | 4.53 | 102.2 → 99.4 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.95 | 5.92 | 100% | 1.32 | 2.23 | 4.54 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.62 | 2.37 | 6.93 | 100% | 0.59 | 1.90 | 4.44 | 105.0 → 96.6 | 0 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.64 | 2.33 | 44.06 | 100% | 0.59 | 1.90 | 4.66 | 104.9 → 96.6 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.62 | 2.25 | 3.00 | 100% | 0.57 | 1.85 | 4.22 | 104.2 → 95.9 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 2.23 | 2.58 | 100% | 0.49 | 1.86 | 4.22 | 82.6 → 77.2 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.64 | 2.31 | 52.30 | 100% | 0.55 | 1.93 | 4.43 | 107.4 → 99.4 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.63 | 2.77 | 5.59 | 100% | 0.64 | 2.00 | 3.89 | 107.5 → 99.2 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.17 | 24.03 | 100% | 1.18 | 1.68 | 3.28 | 46.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.205 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 2.41 | 2.52 | 100% | 0.45 | 1.87 | 4.73 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 262 frames at 120 Hz; the text stood still on 0 of them, 196 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.29 | 3.36 | 100% | 0.57 | 1.90 | 4.56 | 103.3 → 99.9 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 4.53 | 84.85 | 99% | 0.72 | 2.73 | 4.61 | 308.9 → 301.0 | 3 | 1.1 / 4 |

