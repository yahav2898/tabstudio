# wpf-fit progress notes

Task: WPF (OpenSilver) web preview fit modes Fit/100%/Fill, pinch zoom, per-project memory, tests, real repo.

## Findings
- OpenSilver 3.3.3 computes pointer positions as pageX - windowRect.left (cshtml5.js invokePointerCallback) and
  measures text with getBoundingClientRect on a measurer INSIDE the window root; TransformToVisual uses bounding rects.
  => a CSS transform on the window breaks clicks (ButtonBase.IsValidMousePosition) and text measurement.
- Chosen design: scale with the browser's own page scale (meta viewport width/initial-scale; the visual viewport),
  which is invisible to layout and JS coordinates. Android WebView needs UseWideViewPort/LoadWithOverviewMode/
  BuiltInZoomControls. Headless Chrome needs mobile emulation to honor the meta viewport.

## Done
- (nothing yet)

## Next
- experiment locally with /opt/pw-browsers chromium + puppeteer (mock page)
