/* SafePath BD — motion layer.
   Scroll reveals, hero route measurement and pointer parallax.
   All effects are decorative: everything stays usable when they are skipped. */
(function () {
    "use strict";

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    
    function initReveals() {
        const elements = Array.prototype.slice.call(document.querySelectorAll("[data-reveal]"));
        if (!elements.length) {
            return;
        }

        if (reduceMotion.matches || !("IntersectionObserver" in window)) {
            revealAll(elements);
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { rootMargin: "0px 0px -12% 0px", threshold: 0.15 });

        elements.forEach(function (el, index) {
            const group = el.getAttribute("data-reveal");
            if (group === "stagger") {
                el.style.setProperty("--reveal-delay", (index % 6) * 70 + "ms");
            }
            observer.observe(el);
        });
    }

    // The dash pattern must match the real path length or the draw-in looks arbitrary.
    function initRouteDraw() {
        const path = document.querySelector("[data-route-path]");
        if (!path || typeof path.getTotalLength !== "function") {
            return;
        }

        const length = Math.ceil(path.getTotalLength());
        path.style.setProperty("--rv-length", length);
    }

    function initHeroParallax() {
        const visual = document.querySelector("[data-parallax]");
        if (!visual || reduceMotion.matches || window.matchMedia("(hover: none)").matches) {
            return;
        }

        let frame = 0;

        visual.addEventListener("pointermove", function (event) {
            if (frame) {
                return;
            }

            frame = window.requestAnimationFrame(function () {
                frame = 0;
                const bounds = visual.getBoundingClientRect();
                const x = (event.clientX - bounds.left) / bounds.width - 0.5;
                const y = (event.clientY - bounds.top) / bounds.height - 0.5;
                visual.style.transform =
                    "perspective(900px) rotateX(" + (-y * 4).toFixed(2) + "deg) rotateY(" + (x * 5).toFixed(2) + "deg)";
            });
        });

        visual.addEventListener("pointerleave", function () {
            visual.style.transform = "";
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initRouteDraw();
        initReveals();
        initHeroParallax();
    });
})();
