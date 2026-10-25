# Layout checks

After every start or restart of an Avalonia app, TabStudio checks the layout of every screen the app runs on: the
app preview, each virtual device in the Device Matrix, and each phone or tablet in the swarm. What it finds goes to
the Error List, one row per element and problem, with the screens it shows on, a picture of each, and the XAML
element as its location. Tapping a row opens the element in the editor and outlines it in the Device Matrix;
**Fix with Agent** sends Claude the problem, the element's XAML and a picture of the screen.

Each check can be turned off in the Device Matrix's toolbar, under **Layout checks**. The checks look at the laid-out
view the way a designer would, and leave alone what is fine by design, such as content in a scroll viewer that
scrolls into view.

| Code | Check | Finds |
| --- | --- | --- |
| [SWRM001](#swrm001) | Clipped text | Text cut off, overflowing, or trimmed with an ellipsis |
| [SWRM002](#swrm002) | Off screen | Controls outside the screen, or outside what their scroll viewer reaches |
| [SWRM003](#swrm003) | Overlapping controls | Controls that take taps lying over each other |
| [SWRM004](#swrm004) | Small touch targets | Controls under 44 × 44 on phones and tablets, 24 × 24 on desktops |
| [SWRM005](#swrm005) | Low contrast | Text below WCAG AA contrast |

Sizes are in device-independent pixels (dips), the unit Avalonia lays views out in.

## SWRM001

**Clipped text.** Text that does not fit the room its layout gives it:

- it is **trimmed** with an ellipsis (a warning: the layout chose to trim, but people cannot read all of it);
- it **runs past its box**, because it does not wrap and its box is narrower than the text (an error);
- it is **cut off at the bottom**, because its box is too short for its lines or its `MaxLines` (an error);
- it is **cut off by its container** or by the edge of the screen (an error).

Text that a scroll viewer scrolls into view is not clipped. Text inside a control that is already reported off
screen (SWRM002) is not reported again.

**How to fix it:** let the text wrap (`TextWrapping="Wrap"`), give it the room it needs with star sizes or
`MinWidth`, or shorten it. Shrinking the font hides the problem from people with large text settings: try the
Device Matrix's **Large text** button on a virtual device.

## SWRM002

**Off screen.** A control or an image that a person cannot reach:

- wholly **outside the screen** (an error), such as a button pushed off by a fixed width or a negative margin;
- partly outside the screen (a warning), such as a text box wider than a small phone;
- **beyond what its scroll viewer can scroll to**, or **hidden by a container that clips it**.

Items below the fold of a list are reachable: their scroll viewer brings them into view. Only the outermost element
that is cut is reported, not everything inside it.

**How to fix it:** make the layout adapt to narrow screens, with star sizes, a `WrapPanel`, or a `ScrollViewer`
around content that can be longer than the screen, instead of fixed widths and offsets.

## SWRM003

**Overlapping controls.** Two controls a person can tap that lie over each other, so a tap meant for the one below
goes to the one on top. The row names the control below, and the message names the one that covers it. An overlap of
a quarter or more of the smaller control is an error; a smaller one is a warning.

A control inside another, such as a button in a list item, is not an overlap, and neither is content that scrolls
under a control that stays, such as a list under a floating button. Controls whose borders only touch never count.

**How to fix it:** give each control its own space with the layout panels, rather than margins or render
transforms that push one over the other.

## SWRM004

**Small touch targets.** A control smaller than a finger can hit reliably. On phones, large phones and tablets, a
touch target needs at least **44 × 44** dips, as Apple's and Google's guidelines ask. In a desktop window, used with
a mouse, it needs **24 × 24**, WCAG 2.2's minimum target size. A control under half the minimum on either side is an
error; otherwise it is a warning.

Avalonia's Fluent controls are 32 dips tall, made for a mouse and touch alike; on a phone they are small targets.

**How to fix it:** set `MinHeight` and `MinWidth` to 44 on touch screens, or add padding, so the control is easy to
hit without changing how it looks much.

## SWRM005

**Low contrast.** Text whose color is too close to what is behind it to read comfortably, below WCAG AA: **4.5:1**
for normal text, **3:1** for large text (18 points, 24 dips, or 14 points bold). The colors are the ones a person
sees: the text's brush and its opacity, and the element's and its parents' opacity, over the backgrounds of the
elements around it, blended as they are drawn. Text below 3:1 (2:1 for large text) is an error; otherwise it is a
warning.

Text on a gradient or a picture is left alone, since its contrast changes from one letter to the next. Text of a
disabled control is exempt, as WCAG exempts inactive controls.

**How to fix it:** use foreground and background colors with enough contrast, preferably the theme's own resources,
which keep their contrast in light and dark.
