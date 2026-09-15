/* SafePath BD — shared UI behaviour (navbar, disclosure controls). */
(function () {
    "use strict";

    function initNavbar() {
        const navbar = document.querySelector("[data-navbar]");
        if (!navbar) {
            return;
        }

        const toggle = navbar.querySelector("[data-nav-toggle]");
        const applyStuckState = () => navbar.classList.toggle("is-stuck", window.scrollY > 8);

        applyStuckState();
        window.addEventListener("scroll", applyStuckState, { passive: true });

        if (toggle) {
            const closeMenu = function () {
                navbar.classList.remove("is-open");
                toggle.setAttribute("aria-expanded", "false");
            };

            toggle.addEventListener("click", function () {
                const open = navbar.classList.toggle("is-open");
                toggle.setAttribute("aria-expanded", String(open));
            });

            navbar.querySelectorAll(".nav-links a, .nav-actions a").forEach(function (link) {
                link.addEventListener("click", closeMenu);
            });

            document.addEventListener("keydown", function (event) {
                if (event.key === "Escape" && navbar.classList.contains("is-open")) {
                    closeMenu();
                    toggle.focus();
                }
            });
        }
    }

    function initPasswordToggles() {
        document.querySelectorAll("[data-password-toggle]").forEach(function (button) {
            const input = document.getElementById(button.getAttribute("data-password-toggle"));
            if (!input) {
                return;
            }

            button.addEventListener("click", function () {
                const reveal = input.type === "password";
                input.type = reveal ? "text" : "password";
                button.setAttribute("aria-label", reveal ? "Hide password" : "Show password");
                button.setAttribute("aria-pressed", String(reveal));
            });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initNavbar();
        initPasswordToggles();
    });
})();
