// Makes a web page one of TabStudio's Swarm screens. The web preview's screen (Swarm/Web/WebAppScreen.cs) runs this
// once per page, then calls window.__tabStudioScreen: state() for the page's size and what a person did on it,
// point() for where to touch an element, type(), scroll(), expect() before a replayed input, and inspect() for the
// layout checks (docs/layout-checks.md), worded as the Avalonia checks word them.
//
// Elements are named by path from body: tag#id where the id is unique, otherwise tag[index] among the siblings of
// the same tag, such as body/main[0]/form#form/button[0]. Running this again on the same page does nothing.
(function () {
  'use strict';
  if (window.__tabStudioScreen) {
    return true;
  }

  var INTERACTIVE = 'a[href],button,input:not([type=hidden]),select,textarea,summary,[role=button],[role=link],[role=checkbox],' +
    '[role=radio],[role=switch],[role=tab],[role=menuitem],[role=option],[onclick],[tabindex]:not([tabindex="-1"]),' +
    '[contenteditable=""],[contenteditable=true]';
  var SKIPPED = { SCRIPT: 1, STYLE: 1, TEMPLATE: 1, NOSCRIPT: 1, BR: 1, WBR: 1, META: 1, LINK: 1, TITLE: 1, HEAD: 1, OPTION: 1, OPTGROUP: 1 };
  var KEYS = {
    Enter: 'Enter', Tab: 'Tab', Escape: 'Escape', Backspace: 'Back', Delete: 'Delete', Insert: 'Insert',
    ArrowLeft: 'Left', ArrowRight: 'Right', ArrowUp: 'Up', ArrowDown: 'Down',
    Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown', ' ': 'Space'
  };
  var CLIPS = /(hidden|clip)/;
  var SCROLLS = /(auto|scroll|overlay)/;
  var SETTLE = 220;
  var EXPECT = 2500;

  var queue = [];
  var recent = [];
  var expected = [];
  var scrolls = [];
  var positions = new WeakMap();
  var quietScrollUntil = 0;
  var personUntil = 0;

  // ---- Element paths: tag#id where the id is unique, otherwise tag[index] among siblings of the same tag.

  function segment(el) {
    var tag = el.tagName.toLowerCase();
    if (el.id && !/[\/#\[\]"\\]/.test(el.id) && document.querySelectorAll('[id="' + el.id + '"]').length === 1) {
      return tag + '#' + el.id;
    }
    var index = 0;
    for (var s = el.previousElementSibling; s; s = s.previousElementSibling) {
      if (s.tagName === el.tagName) {
        index++;
      }
    }
    return tag + '[' + index + ']';
  }

  function describe(el) {
    var parts = [];
    for (var e = el; e && e !== document.body; e = e.parentElement) {
      if (e === document.documentElement) {
        return 'body';
      }
      parts.unshift(segment(e));
    }
    parts.unshift('body');
    return parts.join('/');
  }

  function resolve(path) {
    var parts = String(path || '').split('/');
    if (parts[0] !== 'body' || !document.body) {
      return null;
    }
    var el = document.body;
    for (var i = 1; i < parts.length && el; i++) {
      var m = /^([^#\[\]\/]+)(?:#([^\/]+)|\[(\d+)\])$/.exec(parts[i]);
      if (!m) {
        return null;
      }
      var tag = m[1];
      if (m[2] !== undefined) {
        // An id names the element wherever it moved to.
        var byId = document.getElementById(m[2]);
        el = byId && byId.tagName.toLowerCase() === tag ? byId : null;
        continue;
      }
      var n = Number(m[3]);
      var next = null;
      for (var c = el.firstElementChild; c; c = c.nextElementSibling) {
        if (c.tagName.toLowerCase() === tag && n-- === 0) {
          next = c;
          break;
        }
      }
      el = next;
    }
    return el;
  }

  // ---- Names and sizes, worded as the Avalonia checks word them.

  function quote(text) {
    var line = String(text).replace(/\s+/g, ' ').trim();
    return line.length <= 32 ? '"' + line + '"' : '"' + line.slice(0, 31).trim() + '\u2026"';
  }

  function ownText(el) {
    var text = '';
    for (var n = el.firstChild; n; n = n.nextSibling) {
      if (n.nodeType === 3) {
        text += n.nodeValue;
      }
    }
    return text.replace(/\s+/g, ' ').trim();
  }

  function isInteractive(el) {
    return el.matches(INTERACTIVE);
  }

  function label(el) {
    var tag = el.tagName.toLowerCase();
    var text = (el.textContent || '').replace(/\s+/g, ' ').trim();
    var hint = el.getAttribute('aria-label') || el.getAttribute('title') || el.getAttribute('alt') || el.getAttribute('placeholder');
    if (isInteractive(el)) {
      return text ? tag + ' ' + quote(text) : el.id ? tag + ' ' + el.id : hint ? tag + ' ' + quote(hint) : tag;
    }
    return text ? quote(text) : el.id ? tag + ' ' + el.id : hint ? tag + ' ' + quote(hint) : tag;
  }

  function round(v) {
    return String(Math.round(v));
  }

  function size(w, h) {
    return round(w) + ' \u00d7 ' + round(h);
  }

  function box(r) {
    return { x: r.left, y: r.top, width: r.width, height: r.height };
  }

  // ---- Capture: what a person does on the page, by element, for the other screens to replay.

  function now() {
    return Date.now();
  }

  function expect(kind, value) {
    expected.push({ kind: kind, value: value, until: now() + EXPECT });
  }

  function expectedNow(kind, value) {
    var t = now();
    expected = expected.filter(function (e) { return e.until > t; });
    for (var i = 0; i < expected.length; i++) {
      if (expected[i].kind === kind && (expected[i].value === null || expected[i].value === value)) {
        expected.splice(i, 1);
        return true;
      }
    }
    return false;
  }

  function emit(input) {
    if (!expectedNow(input.kind, input.kind === 'Text' ? input.text : input.kind === 'Key' ? input.key : null)) {
      queue.push(input);
      if (queue.length > 200) {
        queue.shift();
      }
    }
  }

  function aimedAt(target) {
    var el = target && target.nodeType === 1 ? target : target && target.parentElement;
    if (!el || !document.body || !document.body.contains(el)) {
      return null;
    }
    return el.closest(INTERACTIVE) || el;
  }

  function pointerInput(kind, e) {
    var el = aimedAt(e.target);
    if (!el) {
      return;
    }
    var r = el.getBoundingClientRect();
    var x = r.width > 0 ? (e.clientX - r.left) / r.width : 0.5;
    var y = r.height > 0 ? (e.clientY - r.top) / r.height : 0.5;
    emit({ kind: kind, path: describe(el), x: Math.min(1, Math.max(0, x)), y: Math.min(1, Math.max(0, y)) });
  }

  function keyName(e) {
    if (/^Key[A-Z]$/.test(e.code)) {
      return e.code.slice(3);
    }
    if (/^Digit[0-9]$/.test(e.code)) {
      return 'D' + e.code.slice(5);
    }
    if (/^F([1-9]|1[0-2])$/.test(e.key)) {
      return e.key;
    }
    return KEYS[e.key] || null;
  }

  function modifiers(e) {
    var list = [];
    if (e.altKey) {
      list.push('Alt');
    }
    if (e.ctrlKey) {
      list.push('Control');
    }
    if (e.shiftKey) {
      list.push('Shift');
    }
    if (e.metaKey) {
      list.push('Meta');
    }
    return list.length ? list.join(', ') : null;
  }

  function scrollTargetOf(target) {
    return target === document || target === document.documentElement || target === document.body ? document.scrollingElement || document.documentElement : target;
  }

  function positionOf(el) {
    return { x: el.scrollLeft, y: el.scrollTop };
  }

  // Where every scrollable element under a finger is, before the finger moves it.
  function remember(target) {
    for (var el = target && target.nodeType === 1 ? target : null; el; el = el.parentElement) {
      positions.set(el, positionOf(el));
    }
    var root = document.scrollingElement || document.documentElement;
    positions.set(root, positionOf(root));
  }

  // A touch, wheel or key: what scrolls in the next moments scrolls because of it. When it is a replayed input, or
  // its consequence, such as the page scrolling a focused box above a soft keyboard, the scroll is not a person's.
  function touched(target) {
    var t = now();
    var replaying = expected.some(function (e) { return e.until > t && e.kind !== 'Text'; });
    if (replaying) {
      quietScrollUntil = t + 1500;
    } else {
      personUntil = t + 900;
    }
    if (target) {
      remember(target);
    }
  }

  function onScroll(e) {
    var el = scrollTargetOf(e.target);
    if (!el || el.nodeType !== 1) {
      return;
    }
    var at = positionOf(el);
    var before = positions.get(el);
    positions.set(el, at);
    var pending = null;
    for (var i = 0; i < scrolls.length; i++) {
      if (scrolls[i].el === el) {
        pending = scrolls[i];
      }
    }
    // Only a scroll a person started, or the rest of one, such as a fling.
    if (!before || now() < quietScrollUntil || (!pending && now() > personUntil)) {
      return;
    }
    if (!pending) {
      pending = { el: el, dx: 0, dy: 0, timer: 0 };
      scrolls.push(pending);
    }
    pending.dx += at.x - before.x;
    pending.dy += at.y - before.y;
    clearTimeout(pending.timer);
    pending.timer = setTimeout(function () {
      scrolls.splice(scrolls.indexOf(pending), 1);
      if (pending.dx !== 0 || pending.dy !== 0) {
        var root = document.scrollingElement || document.documentElement;
        emit({ kind: 'Scroll', path: pending.el === root ? 'body' : describe(pending.el), dx: pending.dx, dy: pending.dy });
      }
    }, SETTLE);
  }

  function listen() {
    document.addEventListener('pointerdown', function (e) { touched(e.target); }, true);
    ['touchstart', 'touchend', 'click', 'contextmenu', 'focusin', 'scroll', 'compositionend', 'input'].forEach(function (type) {
      document.addEventListener(type, note, { capture: true, passive: true });
    });
    // Not passive: the browser waits for these before it scrolls, so the positions are the ones before the scroll.
    document.addEventListener('touchstart', function (e) { touched(e.target); }, { capture: true, passive: false });
    document.addEventListener('touchmove', function () { touched(null); }, { capture: true, passive: true });
    document.addEventListener('wheel', function (e) { touched(e.target); }, { capture: true, passive: false });
    document.addEventListener('keydown', function () { touched(document.activeElement); }, true);
    document.addEventListener('scroll', onScroll, true);
    document.addEventListener('click', function (e) {
      // A click from the keyboard comes with its key, which is captured itself.
      if (e.isTrusted && e.detail > 0) {
        pointerInput('Tap', e);
      }
    }, true);
    document.addEventListener('contextmenu', function (e) {
      if (e.isTrusted) {
        pointerInput('LongPress', e);
      }
    }, true);
    document.addEventListener('keydown', function (e) {
      if (!e.isTrusted || e.isComposing || /^(Shift|Control|Alt|Meta|Unidentified|Process|Dead)$/.test(e.key)) {
        return;
      }
      var command = e.ctrlKey || e.altKey || e.metaKey || e.key.length !== 1;
      var name = command ? keyName(e) : null;
      if (name) {
        emit({ kind: 'Key', path: '', key: name, modifiers: modifiers(e) });
      }
    }, true);
    document.addEventListener('beforeinput', function (e) {
      // Keys of a soft keyboard come as input rather than as keys.
      if (e.isTrusted && !e.isComposing && e.inputType === 'deleteContentBackward' && !e.data) {
        emit({ kind: 'Key', path: '', key: 'Back', modifiers: null, soft: true });
      }
    }, true);
    document.addEventListener('input', function (e) {
      if (e.isTrusted && !e.isComposing && (e.inputType === 'insertText' || e.inputType === 'insertFromPaste') && e.data) {
        emit({ kind: 'Text', path: '', text: e.data });
      }
    }, true);
    document.addEventListener('compositionend', function (e) {
      if (e.data) {
        emit({ kind: 'Text', path: '', text: e.data });
      }
    }, true);
    remember(document.body);
  }

  // What the page saw lately, for finding out why a replayed input did not do what it should.
  function note(e) {
    var touch = e.changedTouches && e.changedTouches[0];
    var target = e.target && e.target.nodeType === 1 ? e.target : null;
    recent.push({
      type: e.type,
      path: target ? describe(target) : String(e.target && e.target.nodeName),
      x: Math.round(touch ? touch.clientX : e.clientX || 0),
      y: Math.round(touch ? touch.clientY : e.clientY || 0),
      trusted: e.isTrusted,
      data: e.data || null
    });
    if (recent.length > 24) {
      recent.shift();
    }
  }

  // ---- Replay: where to touch an element, text into the focused element, and scrolls.

  function point(path, fx, fy) {
    var el = resolve(path);
    if (!el) {
      return null;
    }
    var r = el.getBoundingClientRect();
    if (r.bottom <= 0 || r.top >= innerHeight || r.right <= 0 || r.left >= innerWidth) {
      quietScrollUntil = now() + 600;
      el.scrollIntoView({ block: 'center', inline: 'center' });
      r = el.getBoundingClientRect();
    }
    if (!fx && !fy) {
      fx = 0.5;
      fy = 0.5;
    }
    var x = r.left + r.width * fx;
    var y = r.top + r.height * fy;
    var viewport = window.visualViewport;
    var zoom = viewport ? viewport.scale : 1;
    var left = viewport ? viewport.offsetLeft : 0;
    var top = viewport ? viewport.offsetTop : 0;
    return { x: (x - left) * zoom * devicePixelRatio, y: (y - top) * zoom * devicePixelRatio };
  }

  function type(text) {
    var el = document.activeElement;
    if (!el || el === document.body) {
      return false;
    }
    expect('Text', text);
    if (document.execCommand('insertText', false, text)) {
      return true;
    }
    if ('value' in el) {
      el.value += text;
      el.dispatchEvent(new Event('input', { bubbles: true }));
      return true;
    }
    return false;
  }

  function scroll(path, dx, dy) {
    var el = path === 'body' ? null : resolve(path);
    if (path !== 'body' && !el) {
      return false;
    }
    quietScrollUntil = now() + 600;
    for (var target = el; target; target = target.parentElement) {
      var s = getComputedStyle(target);
      if ((SCROLLS.test(s.overflowY) && target.scrollHeight > target.clientHeight && dy) ||
        (SCROLLS.test(s.overflowX) && target.scrollWidth > target.clientWidth && dx)) {
        target.scrollBy(dx, dy);
        return true;
      }
    }
    window.scrollBy(dx, dy);
    return true;
  }

  // ---- The page as a screen.

  function parseColor(text) {
    var m = /rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:[\s,\/]+([\d.]+%?))?\s*\)/.exec(text || '');
    if (!m) {
      return null;
    }
    var a = m[4] === undefined ? 1 : m[4].slice(-1) === '%' ? parseFloat(m[4]) / 100 : parseFloat(m[4]);
    return { r: +m[1], g: +m[2], b: +m[3], a: a };
  }

  function over(top, below) {
    var a = top.a;
    return { r: top.r * a + below.r * (1 - a), g: top.g * a + below.g * (1 - a), b: top.b * a + below.b * (1 - a), a: 1 };
  }

  function channel(c) {
    c /= 255;
    return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  }

  function luminance(c) {
    return 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
  }

  function ratio(a, b) {
    var la = luminance(a);
    var lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
  }

  function canvasColor() {
    var scheme = getComputedStyle(document.documentElement).colorScheme || '';
    var dark = /dark/.test(scheme) && !/light/.test(scheme) || /dark/.test(scheme) && matchMedia('(prefers-color-scheme: dark)').matches;
    return dark ? { r: 18, g: 18, b: 18, a: 1 } : { r: 255, g: 255, b: 255, a: 1 };
  }

  // The color behind an element, its own background and those around it blended as they are drawn; null on a
  // gradient or a picture.
  function backgroundOf(el) {
    var layers = [];
    for (var e = el; e; e = e.parentElement) {
      var s = getComputedStyle(e);
      if (s.backgroundImage && s.backgroundImage !== 'none') {
        return null;
      }
      var c = parseColor(s.backgroundColor);
      if (c && c.a > 0) {
        c.a *= opacityBetween(el, e);
        layers.push(c);
        if (c.a >= 1) {
          break;
        }
      }
    }
    var color = layers.length && layers[layers.length - 1].a >= 1 ? layers.pop() : canvasColor();
    while (layers.length) {
      color = over(layers.pop(), color);
    }
    return color;
  }

  function opacityBetween(el, ancestor) {
    var o = 1;
    for (var e = el; e && e !== ancestor; e = e.parentElement) {
      o *= parseFloat(getComputedStyle(e).opacity);
    }
    return o;
  }

  // The screen's size in the page's pixels: the layout viewport, which a page wider than the screen does not widen.
  function screenSize() {
    var root = document.documentElement;
    return { width: root.clientWidth || innerWidth, height: root.clientHeight || innerHeight };
  }

  function state() {
    var inputs = queue;
    queue = [];
    var background = document.body ? backgroundOf(document.body) : canvasColor();
    var viewport = screenSize();
    return {
      ready: document.readyState === 'complete' && !!document.body,
      url: location.href,
      width: viewport.width,
      height: viewport.height,
      scale: devicePixelRatio,
      dark: luminance(background) < 0.2,
      rtl: getComputedStyle(document.documentElement).direction === 'rtl',
      inputs: inputs
    };
  }

  // ---- Layout checks: the five rules of the Avalonia checks, on the page as the browser laid it out.

  function scan() {
    var items = [];
    var all = document.body ? document.body.getElementsByTagName('*') : [];
    var alpha = new Map();
    for (var i = 0; i < all.length; i++) {
      var el = all[i];
      if (SKIPPED[el.tagName] || el.ownerSVGElement) {
        continue;
      }
      var s = getComputedStyle(el);
      var parentAlpha = alpha.has(el.parentElement) ? alpha.get(el.parentElement) : 1;
      var a = parentAlpha * parseFloat(s.opacity);
      if (el.getAttribute('aria-hidden') === 'true' || s.display === 'none') {
        a = 0;
      }
      alpha.set(el, a);
      if (a <= 0.01 || s.visibility !== 'visible' || el.getClientRects().length === 0) {
        continue;
      }
      var r = el.getBoundingClientRect();
      items.push({
        el: el,
        style: s,
        rect: r,
        alpha: a,
        fixed: s.position === 'fixed' || s.position === 'sticky',
        hidden: r.width * r.height <= 1 || /rect\(0(px)?,? 0(px)?,? 0(px)?,? 0(px)?\)/.test(s.clip) || /inset\(50%\)/.test(s.clipPath)
      });
    }
    return items;
  }

  // The nearest ancestor that clips the element, or lets it scroll.
  function containerOf(el, style) {
    if (style.position === 'fixed') {
      return null;
    }
    var escapes = style.position === 'absolute' ? el.offsetParent : null;
    for (var a = el.parentElement; a && a !== document.body && a !== document.documentElement; a = a.parentElement) {
      if (escapes && a !== escapes && escapes.contains(a)) {
        continue;
      }
      var s = getComputedStyle(a);
      if (s.overflowX !== 'visible' || s.overflowY !== 'visible') {
        return { el: a, style: s, scrolls: SCROLLS.test(s.overflowX) || SCROLLS.test(s.overflowY) };
      }
      if (s.position === 'fixed') {
        return null;
      }
    }
    return null;
  }

  function edge(r, area, outside) {
    return r.left >= area.right ? round(r.left - area.right) + ' dips past ' + outside + 'right edge'
      : r.right <= area.left ? round(area.left - r.right) + ' dips past ' + outside + 'left edge'
      : r.top >= area.bottom ? round(r.top - area.bottom) + ' dips below ' + outside + 'bottom edge'
      : round(area.top - r.bottom) + ' dips above ' + outside + 'top edge';
  }

  function issue(kind, severity, el, message, r) {
    return { kind: kind, severity: severity, path: describe(el), message: message, bounds: box(r || el.getBoundingClientRect()) };
  }

  // Text, rather than a control or a picture: text that is cut is clipped text, reported by its own rule.
  function isText(el) {
    return !!ownText(el) && !isInteractive(el);
  }

  function offScreen(items, page, out) {
    var reported = [];
    var gone = [];
    items.forEach(function (item) {
      var el = item.el;
      var r = item.rect;
      if (item.hidden || r.width <= 0 || r.height <= 0 || reported.some(function (p) { return p.contains(el); })) {
        return;
      }
      var lbl = label(el);
      var container = containerOf(el, item.style);
      var found = null;
      var wholly = false;
      if (container) {
        var c = container.el.getBoundingClientRect();
        var area = container.scrolls
          ? { left: c.left - container.el.scrollLeft, top: c.top - container.el.scrollTop, right: c.left - container.el.scrollLeft + container.el.scrollWidth, bottom: c.top - container.el.scrollTop + container.el.scrollHeight }
          : { left: c.left, top: c.top, right: c.right, bottom: c.bottom };
        var what = container.scrolls ? 'beyond what its scroll area reaches' : 'hidden by its container';
        if (r.right <= area.left + 0.5 || r.left >= area.right - 0.5 || r.bottom <= area.top + 0.5 || r.top >= area.bottom - 0.5) {
          wholly = true;
          found = issue('OffScreen', 'Error', el, lbl + ' is ' + what + ' (' + edge(r, area, 'its ') + '), so it cannot be seen or tapped.');
        } else if (!container.scrolls && !isText(el)) {
          var hx = Math.max(0, area.left - r.left) + Math.max(0, r.right - area.right);
          var hy = Math.max(0, area.top - r.top) + Math.max(0, r.bottom - area.bottom);
          if (hx > 1 || hy > 1) {
            found = issue('OffScreen', 'Warning', el, lbl + ' is partly hidden by its container: ' + (hx >= hy
              ? round(hx) + ' of its ' + round(r.width) + ' dips of width are cut off.'
              : round(hy) + ' of its ' + round(r.height) + ' dips of height are cut off.'));
          }
        }
      }
      if (!found) {
        // The screen: as wide as the viewport, since a page that scrolls sideways is a page that does not fit, and
        // as long as the page, which scrolls down.
        var screen = item.fixed
          ? { left: 0, top: 0, right: page.width, bottom: page.height }
          : { left: -page.x, top: -page.y, right: page.width - page.x, bottom: page.scrollHeight - page.y };
        if (r.right <= screen.left + 0.5 || r.left >= screen.right - 0.5 || r.bottom <= screen.top + 0.5 || r.top >= screen.bottom - 0.5) {
          wholly = true;
          found = issue('OffScreen', 'Error', el, lbl + ' is outside the screen (' + edge(r, screen, 'the ') + '), so it cannot be seen or tapped.');
        } else if (!(page.clipsX && isText(el))) {
          var cutLeft = Math.max(0, screen.left - r.left);
          var cutRight = Math.max(0, r.right - screen.right);
          var cutTop = Math.max(0, screen.top - r.top);
          if (cutLeft + cutRight > 1) {
            found = issue('OffScreen', 'Warning', el, lbl + ' is partly outside the screen: ' + round(cutLeft + cutRight) + ' of its ' + round(r.width) +
              ' dips of width are cut off' + (cutRight > 1 && !page.clipsX ? ', and the page scrolls sideways.' : '.'));
          } else if (cutTop > 1) {
            found = issue('OffScreen', 'Warning', el, lbl + ' is partly outside the screen: ' + round(cutTop) + ' of its ' + round(r.height) + ' dips of height are cut off.');
          }
        }
      }
      if (found) {
        reported.push(el);
        if (wholly) {
          gone.push(el);
        }
        out.push(found);
      }
    });
    return { reported: reported, gone: gone };
  }

  function textRect(el) {
    var range = document.createRange();
    var rect = null;
    for (var n = el.firstChild; n; n = n.nextSibling) {
      if (n.nodeType === 3 && n.nodeValue.trim()) {
        range.selectNodeContents(n);
        var r = range.getBoundingClientRect();
        rect = rect ? { left: Math.min(rect.left, r.left), top: Math.min(rect.top, r.top), right: Math.max(rect.right, r.right), bottom: Math.max(rect.bottom, r.bottom) } : { left: r.left, top: r.top, right: r.right, bottom: r.bottom };
      }
    }
    return rect;
  }

  function clippedText(items, page, offScreenElements, profile, out) {
    items.forEach(function (item) {
      var el = item.el;
      var s = item.style;
      if (!ownText(el) || offScreenElements.some(function (p) { return p.contains(el); })) {
        return;
      }
      var lbl = label(el);
      var inline = s.display === 'inline';
      var clipsX = CLIPS.test(s.overflowX);
      var clipsY = CLIPS.test(s.overflowY);
      if (!inline && el.clientWidth > 0) {
        if (s.textOverflow === 'ellipsis' && clipsX && el.scrollWidth > el.clientWidth + 1) {
          out.push(issue('ClippedText', 'Warning', el, lbl + ' is trimmed with an ellipsis on ' + profile.screen + ': people cannot read all of it.'));
          return;
        }
        if (s.webkitLineClamp && s.webkitLineClamp !== 'none' && el.scrollHeight > el.clientHeight + 1) {
          out.push(issue('ClippedText', 'Warning', el, lbl + ' is trimmed with an ellipsis on ' + profile.screen + ': people cannot read all of it.'));
          return;
        }
        if (clipsX && el.scrollWidth > el.clientWidth + 1) {
          out.push(issue('ClippedText', 'Error', el, lbl + ' runs past its box: it needs ' + round(el.scrollWidth) + ' dips of width and has ' + round(el.clientWidth) + '.'));
          return;
        }
        if (clipsY && el.scrollHeight > el.clientHeight + 1) {
          out.push(issue('ClippedText', 'Error', el, lbl + ' is cut off at the bottom: it needs ' + round(el.scrollHeight) + ' dips of height and has ' + round(el.clientHeight) + '.'));
          return;
        }
      }
      var text = textRect(el);
      if (!text) {
        return;
      }
      var container = containerOf(el, s);
      var area = null;
      var by = null;
      if (container && !container.scrolls) {
        var c = container.el.getBoundingClientRect();
        area = { left: c.left, top: c.top, right: c.right, bottom: c.bottom };
        by = 'its container';
      } else if (!container && page.clipsX && !item.fixed) {
        area = { left: 0, top: -Infinity, right: page.width, bottom: Infinity };
        by = 'the edge of the screen';
      }
      if (area) {
        var hx = Math.max(0, area.left - text.left) + Math.max(0, text.right - area.right);
        var hy = Math.max(0, area.top - text.top) + Math.max(0, text.bottom - area.bottom);
        var w = text.right - text.left;
        var h = text.bottom - text.top;
        if ((hx > 1 || hy > 1) && hx < w && hy < h) {
          out.push(issue('ClippedText', 'Error', el, lbl + ' is cut off by ' + by + ': ' + round(Math.max(hx, hy)) + ' of its ' + size(w, h) + ' dips are hidden.'));
        }
      }
    });
  }

  function interactiveItems(items) {
    return items.filter(function (item) {
      return !item.hidden && isInteractive(item.el) && !item.el.closest(':disabled, [aria-disabled=true]') && item.rect.width > 0 && item.rect.height > 0;
    });
  }

  function overlaps(controls, out) {
    for (var i = 0; i < controls.length; i++) {
      for (var j = i + 1; j < controls.length; j++) {
        var a = controls[i];
        var b = controls[j];
        if (a.el.contains(b.el) || b.el.contains(a.el) || a.fixed !== b.fixed) {
          continue;
        }
        var left = Math.max(a.rect.left, b.rect.left);
        var right = Math.min(a.rect.right, b.rect.right);
        var top = Math.max(a.rect.top, b.rect.top);
        var bottom = Math.min(a.rect.bottom, b.rect.bottom);
        if (right - left <= 0.5 || bottom - top <= 0.5) {
          continue;
        }
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var hit = cx >= 0 && cy >= 0 && cx < innerWidth && cy < innerHeight ? document.elementFromPoint(cx, cy) : null;
        var on = hit && a.el.contains(hit) ? a : hit && b.el.contains(hit) ? b : null;
        if (!on) {
          continue;
        }
        var below = on === a ? b : a;
        var shared = (right - left) * (bottom - top);
        var smaller = Math.min(a.rect.width * a.rect.height, b.rect.width * b.rect.height);
        var topLabel = label(on.el);
        out.push(issue('Overlap', shared >= smaller / 4 ? 'Error' : 'Warning', below.el,
          label(below.el) + ' lies under ' + topLabel + ': ' + size(right - left, bottom - top) + ' dips of them overlap, and a tap there goes to ' + topLabel + '.'));
      }
    }
  }

  function inlineInText(el, style) {
    if (style.display !== 'inline' || !el.parentElement) {
      return false;
    }
    return ownText(el.parentElement).length > 0;
  }

  function touchTargets(controls, profile, out) {
    var minimum = profile.touch;
    controls.forEach(function (item) {
      var el = item.el;
      if (inlineInText(el, item.style)) {
        return;
      }
      var w = item.rect.width;
      var h = item.rect.height;
      // A check box or radio button is also tapped through its label.
      if (el.labels) {
        for (var i = 0; i < el.labels.length; i++) {
          var l = el.labels[i].getBoundingClientRect();
          w = Math.max(w, l.width);
          h = Math.max(h, l.height);
        }
      }
      if (w >= minimum - 0.5 && h >= minimum - 0.5) {
        return;
      }
      var severity = w < minimum / 2 || h < minimum / 2 ? 'Error' : 'Warning';
      var need = profile.formFactor === 'Desktop'
        ? 'in a desktop window, pointer targets need at least ' + size(minimum, minimum)
        : 'on ' + profile.screen + ', touch targets need at least ' + size(minimum, minimum) + ' to be tapped reliably';
      out.push(issue('SmallTouchTarget', severity, el, label(el) + ' is ' + size(item.rect.width, item.rect.height) + ' dips; ' + need + '.'));
    });
  }

  function contrast(items, out) {
    items.forEach(function (item) {
      var el = item.el;
      var s = item.style;
      var hasText = ownText(el) || (/^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) && el.value && el.type !== 'checkbox' && el.type !== 'radio' && el.type !== 'range');
      if (!hasText || item.hidden || el.closest(':disabled, [aria-disabled=true]')) {
        return;
      }
      var background = backgroundOf(el);
      var color = parseColor(s.color);
      if (!background || !color) {
        return;
      }
      color.a *= item.alpha;
      var value = ratio(over(color, background), background);
      var px = parseFloat(s.fontSize);
      var large = px >= 24 || (px >= 18.66 && parseInt(s.fontWeight, 10) >= 700);
      var minimum = large ? 3 : 4.5;
      if (value >= minimum - 0.005) {
        return;
      }
      var severity = value < (large ? 2 : 3) ? 'Error' : 'Warning';
      out.push(issue('LowContrast', severity, el, label(el) + ' has a contrast of ' + value.toFixed(1) + ':1 with its background; ' +
        (large ? 'large text' : 'text this size') + ' needs at least ' + (large ? '3' : '4.5') + ':1 (WCAG AA).'));
    });
  }

  function inspect(profile) {
    if (!document.body) {
      return [];
    }
    var root = document.scrollingElement || document.documentElement;
    var html = getComputedStyle(document.documentElement);
    var body = getComputedStyle(document.body);
    var viewport = screenSize();
    var page = {
      width: viewport.width,
      height: viewport.height,
      x: scrollX,
      y: scrollY,
      scrollHeight: Math.max(root.scrollHeight, innerHeight),
      clipsX: CLIPS.test(html.overflowX) || CLIPS.test(body.overflowX)
    };
    var items = scan();
    var out = [];
    var off = offScreen(items, page, out);
    clippedText(items, page, off.reported, profile, out);

    // What cannot be seen at all is reported once, as off screen.
    var shown = items.filter(function (item) { return !off.gone.some(function (g) { return g.contains(item.el); }); });
    var controls = interactiveItems(shown);
    overlaps(controls, out);
    touchTargets(controls, profile, out);
    contrast(shown, out);
    var seen = {};
    return out.filter(function (i) {
      var key = i.kind + '|' + i.path;
      if (seen[key]) {
        return false;
      }
      seen[key] = true;
      return true;
    });
  }

  window.__tabStudioScreen = {
    recent: function () { return recent.slice(); },
    describe: describe,
    resolve: resolve,
    state: state,
    point: point,
    type: type,
    scroll: scroll,
    expect: expect,
    inspect: inspect
  };
  listen();
  return true;
})();
