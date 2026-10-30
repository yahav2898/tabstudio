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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.35 | 2.55 | 100% | 0.40 | 2.01 | 4.39 | 86.4 → 87.3 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 2.62 | 48.64 | 100% | 0.67 | 2.13 | 4.77 | 124.2 → 121.9 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.01 | 5.06 | 100% | 1.21 | 2.23 | 4.73 | 159.9 → 144.2 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.81 | 28.51 | 100% | 0.66 | 2.24 | 4.60 | 104.0 → 100.9 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.29 | 3.20 | 100% | 0.55 | 1.89 | 4.35 | 81.3 → 73.9 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.40 | 44.59 | 100% | 0.58 | 1.97 | 4.47 | 106.5 → 98.1 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 2.42 | 3.41 | 100% | 0.55 | 1.99 | 4.39 | 106.4 → 98.0 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 3.03 | 4.84 | 100% | 0.73 | 2.19 | 4.60 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.37 | 6.34 | 100% | 0.52 | 2.01 | 4.35 | 83.8 → 78.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 2.36 | 3.35 | 100% | 0.52 | 1.98 | 4.42 | 109.0 → 100.9 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 2.40 | 43.90 | 100% | 0.54 | 2.02 | 4.00 | 109.2 → 100.8 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.07 | 3.08 | 100% | 0.52 | 1.70 | 3.18 | 46.5 → 42.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.57 | 2.65 | 4.76 | 100% | 0.49 | 2.37 | 4.68 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 238 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.43 | 3.37 | 100% | 0.53 | 2.00 | 4.47 | 104.8 → 101.5 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 2.85 | 50.82 | 100% | 0.62 | 2.35 | 4.58 | 318.3 → 310.2 | 1 | 1.1 / 4 |

