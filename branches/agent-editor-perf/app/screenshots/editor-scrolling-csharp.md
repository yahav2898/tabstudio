# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, LineNumberMargin, LiveTestingMargin, Line, FoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.13.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 3.67 | 10.76 | 98% | 0.42 | 2.53 | 3.10 | 307.3 → 305.5 | 7 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 2.90 | 18.39 | 98% | 0.61 | 2.46 | 3.49 | 322.7 → 322.6 | 10 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.90 | 17.66 | 98% | 0.85 | 2.52 | 3.22 | 346.0 → 333.4 | 9 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.79 | 13.28 | 98% | 0.54 | 1.72 | 3.15 | 317.0 → 309.1 | 9 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 1.69 | 8.88 | 99% | 0.43 | 1.21 | 2.82 | 173.2 → 162.3 | 5 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 2.77 | 12.90 | 98% | 0.44 | 2.41 | 3.07 | 281.7 → 271.5 | 8 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.75 | 3.69 | 13.84 | 98% | 0.64 | 2.54 | 3.33 | 316.3 → 305.6 | 9 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.36 | 11.98 | 98% | 0.58 | 1.49 | 3.11 | 310.2 → 299.1 | 8 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.78 | 15.17 | 98% | 0.42 | 2.41 | 2.83 | 263.5 → 260.3 | 7 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 3.63 | 12.51 | 98% | 0.47 | 2.60 | 3.02 | 319.6 → 309.1 | 9 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 2.80 | 13.21 | 98% | 0.45 | 1.36 | 2.57 | 319.9 → 308.9 | 8 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.23 | 1.28 | 7.43 | 100% | 0.40 | 0.91 | 2.36 | 64.3 → 60.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 258.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.291 |
| AttachingStyle - | 0.119 |
| MeasuringLayoutable LineNumberMargin | 0.019 |
| ArrangingLayoutable TextLayer | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable FoldingMarginMarker | 0.004 |
| ArrangingLayoutable FoldingMargin | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable VisualLineDrawingVisual | 0.003 |
| MeasuringLayoutable FoldingMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| RaisingRoutedEvent FoldingMarginMarker | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| RaisingRoutedEvent VisualLineDrawingVisual | 0.002 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.06 | 5.16 | 8.87 | 96% | 2.30 | 3.60 | 7.29 | 326.1 → 339.3 | 2 | 0.5 / 2 |

- Drag: the text moved 0.994 dip per dip of finger movement (1.000 is one to one).
- Starting the drag moved the caret and focused the editor (opening the on-screen keyboard on a tablet).
- Release at 6620 dip/s: glided 110 dip in 25 ms over 1 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

