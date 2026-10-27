// TabStudio's side of a WPF app in the web preview: what the app writes to the console, and its uncaught errors, go
// to TabStudio's Output panel; the title bar follows the window's Title; its close button stops the app.
(function () {
    'use strict';

    var consoleRoute = '_tabstudio/console';
    var queue = [];
    var timer = 0;

    function post(route, body) {
        try {
            fetch(route, { method: 'POST', body: body, headers: { 'Content-Type': 'text/plain;charset=UTF-8' }, keepalive: true });
        } catch (e) {
            // The page is going away, or TabStudio stopped serving it.
        }
    }

    function flush() {
        timer = 0;
        if (queue.length > 0) {
            post(consoleRoute, JSON.stringify(queue.splice(0, queue.length)));
        }
    }

    function describe(value) {
        if (typeof value === 'string') {
            return value;
        }

        if (value instanceof Error) {
            return value.stack || String(value);
        }

        try {
            return JSON.stringify(value);
        } catch (e) {
            return String(value);
        }
    }

    function send(level, values) {
        var parts = [];
        for (var i = 0; i < values.length; i++) {
            parts.push(describe(values[i]));
        }

        queue.push({ level: level, text: parts.join(' ') });
        if (!timer) {
            timer = setTimeout(flush, 40);
        }
    }

    ['log', 'info', 'warn', 'error', 'debug'].forEach(function (level) {
        var original = console[level];
        console[level] = function () {
            send(level, arguments);
            return original.apply(console, arguments);
        };
    });

    window.addEventListener('error', function (e) {
        send('error', [e.error && e.error.stack ? e.error.stack : e.message]);
    });

    window.addEventListener('unhandledrejection', function (e) {
        send('error', [e.reason && e.reason.stack ? e.reason.stack : String(e.reason)]);
    });

    window.addEventListener('pagehide', flush);

    // How the window is shown: 'fit' (whole, scaled down, never up), 'actual' (its declared size, scrolling) or 'fill'
    // (the page's size), from the page's address (?tabstudio-view=fit), which the IDE sets; a page opened without one
    // fills. The scale is the browser's own page zoom, set with the viewport meta tag as a phone's browser applies it:
    // the app's layout and input never see a scale, and the browser maps every touch to the right control at every
    // zoom. This mirrors TabStudio.Core's PreviewLayoutCalculator.
    var viewQuery = 'tabstudio-view';
    var minimumScale = 0.25;
    var maximumScale = 4;
    var view = readView(location.search);
    var declared = null;
    var pane = null;
    var settleTimer = 0;
    var checkTimer = 0;
    var zoomUnsupported = false;

    function readView(search) {
        var match = new RegExp('[?&]' + viewQuery + '=([a-z]+)').exec(search);
        return match && (match[1] === 'fit' || match[1] === 'actual' || match[1] === 'fill') ? match[1] : 'fill';
    }

    function isSize(length) {
        return typeof length === 'number' && isFinite(length) && length >= 1;
    }

    function center(page, length) {
        return Math.max(0, Math.floor((page - length) / 2));
    }

    function compute(mode, paneWidth, paneHeight, windowWidth, windowHeight) {
        var width = isFinite(paneWidth) ? Math.max(1, paneWidth) : 1;
        var height = isFinite(paneHeight) ? Math.max(1, paneHeight) : 1;
        var layout = { mode: mode, paneWidth: width, paneHeight: height, windowWidth: width, windowHeight: height, left: 0, top: 0, scale: 1, minimumScale: 1, maximumScale: maximumScale, fills: true };
        if (mode === 'fill' || !isSize(windowWidth) || !isSize(windowHeight)) {
            return layout;
        }

        layout.fills = false;
        layout.windowWidth = windowWidth;
        layout.windowHeight = windowHeight;
        if (mode === 'actual') {
            layout.left = center(width, windowWidth);
            layout.top = center(height, windowHeight);
            return layout;
        }

        var fit = Math.min(1, Math.min(width / windowWidth, height / windowHeight));
        var scale = Math.max(minimumScale, Math.floor(fit * 1000) / 1000);
        layout.scale = scale;
        layout.minimumScale = scale;
        layout.left = center(width / scale, windowWidth);
        layout.top = center(height / scale, windowHeight);
        return layout;
    }

    // The pane's size in its own pixels, whatever the page's zoom: the visual viewport times its scale.
    function paneSize() {
        var viewport = window.visualViewport;
        return viewport
            ? { width: viewport.width * viewport.scale, height: viewport.height * viewport.scale }
            : { width: document.documentElement.clientWidth, height: document.documentElement.clientHeight };
    }

    function viewportContent(width, scale, minimum, maximum) {
        return 'width=' + width + ', initial-scale=' + scale + ', minimum-scale=' + minimum + ', maximum-scale=' + maximum
            + (minimum === maximum ? '' : ', user-scalable=yes');
    }

    function apply() {
        var frame = document.getElementById('tabstudio-wpf-window');
        var meta = document.getElementById('tabstudio-wpf-viewport');
        if (!frame || !meta) {
            return;
        }

        pane = paneSize();
        var layout = compute(zoomUnsupported && view === 'fit' ? 'actual' : view, pane.width, pane.height,
            declared && declared.width, declared && declared.height);
        window.tabStudioWpf.layout = layout;
        var root = document.documentElement;
        if (layout.fills) {
            root.classList.remove('tabstudio-wpf-sized');
            frame.style.left = frame.style.top = frame.style.width = frame.style.height = '';
        } else {
            root.classList.add('tabstudio-wpf-sized');
            frame.style.left = layout.left + 'px';
            frame.style.top = layout.top + 'px';
            frame.style.width = layout.windowWidth + 'px';
            frame.style.height = layout.windowHeight + 'px';
        }

        // The scale is pinned first (minimum = maximum), which the browser applies even after the user zoomed; once it
        // has, fingers may zoom between the layout's limits.
        var width = layout.scale === 1 ? 'device-width' : String(layout.paneWidth / layout.scale);
        meta.setAttribute('content', viewportContent(width, layout.scale, layout.scale, layout.scale));
        clearTimeout(settleTimer);
        clearTimeout(checkTimer);
        settleTimer = setTimeout(function () {
            meta.setAttribute('content', viewportContent(width, layout.scale, layout.minimumScale, layout.maximumScale));
        }, 100);

        // A browser that ignores the viewport meta tag, such as a desktop one, cannot scale the page: Fit shows the
        // window at its size there.
        if (layout.scale < 1 && window.visualViewport) {
            checkTimer = setTimeout(function () {
                if (window.visualViewport.scale > 0.999) {
                    zoomUnsupported = true;
                    apply();
                }
            }, 800);
        }
    }

    function onResize() {
        var size = paneSize();
        if (!pane || Math.abs(size.width - pane.width) > 1 || Math.abs(size.height - pane.height) > 1) {
            apply();
        }
    }

    // The browser took a finger for a pinch or a pan after the app saw it press: the app is told it moved far away and
    // lifted, which releases a pressed button without clicking it, so the next tap goes where it lands.
    document.addEventListener('pointercancel', function (e) {
        var target = e.target;
        if (!target || !target.dispatchEvent || typeof PointerEvent !== 'function') {
            return;
        }

        var away = { bubbles: true, cancelable: true, pointerId: e.pointerId, pointerType: e.pointerType, isPrimary: e.isPrimary, button: 0, buttons: 0, clientX: -100000, clientY: -100000 };
        target.dispatchEvent(new PointerEvent('pointermove', away));
        target.dispatchEvent(new PointerEvent('pointerup', away));
    }, true);

    window.tabStudioWpf = {
        // The layout applied last, for tests: the same fields as PreviewLayout, in camel case.
        layout: null,

        // The window's declared Width and Height, from the app's window; NaN for a window that declares none.
        setWindowSize: function (width, height) {
            declared = isSize(width) && isSize(height) ? { width: width, height: height } : null;
            apply();
        },

        // Switches the mode in place, and in the page's address, so a reload shows the same.
        setView: function (mode) {
            view = readView('?' + viewQuery + '=' + mode);
            try {
                var url = new URL(location.href);
                url.searchParams.set(viewQuery, view);
                history.replaceState(history.state, '', url.toString());
            } catch (e) {
                // The address stays as it was; only a reload would show the earlier mode.
            }

            apply();
            return view;
        },

        setTitle: function (title) {
            document.title = title;
            var element = document.getElementById('tabstudio-wpf-title');
            if (element) {
                element.textContent = title;
            }

            post('_tabstudio/title', title);
        },

        setBackground: function (color) {
            var client = document.getElementById('tabstudio-wpf-client');
            if (client) {
                client.style.background = color;
            }
        },

        close: function () {
            flush();
            post('_tabstudio/close', '');
            var client = document.getElementById('tabstudio-wpf-client');
            if (client) {
                var stopped = document.createElement('div');
                stopped.className = 'tabstudio-wpf-stopped';
                stopped.textContent = 'The app was closed.';
                client.appendChild(stopped);
            }
        }
    };

    document.addEventListener('DOMContentLoaded', function () {
        var close = document.getElementById('tabstudio-wpf-close');
        if (close) {
            close.addEventListener('click', window.tabStudioWpf.close);
        }

        apply();
        (window.visualViewport || window).addEventListener('resize', onResize);
    });
})();
