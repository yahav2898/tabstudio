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

    window.tabStudioWpf = {
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
    });
})();
