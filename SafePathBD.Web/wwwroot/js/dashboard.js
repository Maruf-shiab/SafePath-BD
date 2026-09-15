/* SafePath BD — dashboard-only micro-interactions. */
(function () {
    "use strict";

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    function animateValue(element) {
        const target = Number(element.getAttribute("data-countup"));
        if (!Number.isFinite(target) || target < 0 || reduceMotion.matches) {
            element.textContent = Number.isFinite(target) ? String(target) : element.textContent;
            return;
        }

        const duration = 520;
        const start = performance.now();

        function frame(now) {
            const progress = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = String(Math.round(target * eased));
            if (progress < 1) {
                requestAnimationFrame(frame);
            }
        }

        requestAnimationFrame(frame);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-countup]").forEach(animateValue);
    });
})();
