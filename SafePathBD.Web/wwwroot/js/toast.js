/* SafePath BD — toast notifications.
   Replaces alert() everywhere; safe to call before DOMContentLoaded. */
(function (global) {
    "use strict";

    var ICONS = {
        info: '<path d="M12 16v-5M12 8h.01" /><circle cx="12" cy="12" r="9" />',
        success: '<path d="M20 6 9 17l-5-5" />',
        warning: '<path d="M12 9v4M12 17h.01" /><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z" />',
        error: '<path d="M15 9l-6 6M9 9l6 6" /><circle cx="12" cy="12" r="9" />'
    };

    var region = null;

    function ensureRegion() {
        if (region && document.body.contains(region)) {
            return region;
        }

        region = document.createElement("div");
        region.className = "toast-region";
        region.setAttribute("role", "status");
        region.setAttribute("aria-live", "polite");
        document.body.appendChild(region);
        return region;
    }

    function dismiss(toast) {
        if (!toast || toast.dataset.closing === "1") {
            return;
        }

        toast.dataset.closing = "1";
        toast.classList.add("is-leaving");
        window.setTimeout(function () {
            toast.remove();
        }, 220);
    }

    function show(message, options) {
        if (!message) {
            return;
        }

        var opts = options || {};
        var variant = ICONS[opts.variant] ? opts.variant : "info";
        var host = ensureRegion();

        var toast = document.createElement("div");
        toast.className = "toast toast--" + variant;
        toast.innerHTML =
            '<span class="toast-icon" aria-hidden="true">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
            ICONS[variant] + "</svg></span>" +
            '<div class="toast-body"></div>' +
            '<button class="toast-close" type="button" aria-label="Dismiss notification">' +
            '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6l12 12M18 6 6 18" /></svg>' +
            "</button>";

        // Assigned as text so provider or server strings can never inject markup.
        toast.querySelector(".toast-body").textContent = message;
        toast.querySelector(".toast-close").addEventListener("click", function () {
            dismiss(toast);
        });

        host.appendChild(toast);

        var timeout = typeof opts.duration === "number" ? opts.duration : 5000;
        if (timeout > 0) {
            window.setTimeout(function () {
                dismiss(toast);
            }, timeout);
        }

        while (host.children.length > 4) {
            host.removeChild(host.firstElementChild);
        }
    }

    global.SafePathToast = {
        show: show,
        info: function (m, o) { show(m, Object.assign({}, o, { variant: "info" })); },
        success: function (m, o) { show(m, Object.assign({}, o, { variant: "success" })); },
        warning: function (m, o) { show(m, Object.assign({}, o, { variant: "warning" })); },
        error: function (m, o) { show(m, Object.assign({}, o, { variant: "error" })); }
    };
})(window);
