/* SafePath BD — in-app notification bell/panel and controlled unread polling. */
(function () {
    "use strict";

    const root = document.querySelector("[data-notification-root]");
    if (!root) return;

    const toggle = root.querySelector("[data-notification-toggle]");
    const panel = root.querySelector("[data-notification-panel]");
    const closeButton = root.querySelector("[data-notification-close]");
    const list = root.querySelector("[data-notification-list]");
    const badge = root.querySelector("[data-notification-badge]");
    const summary = root.querySelector("[data-notification-summary]");
    const readAll = root.querySelector("[data-notification-read-all]");
    const token = root.querySelector('[data-notification-antiforgery] input[name="__RequestVerificationToken"]')?.value || "";
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let unread = Number(root.getAttribute("data-initial-unread")) || 0;
    let closeTimer = 0;

    function typeClass(code) {
        if (code === "REPORT_VERIFIED" || code === "REPORT_RESOLVED") return "is-success";
        if (code === "REPORT_REJECTED" || code === "ACCIDENT_ALERT") return "is-danger";
        if (code === "HAZARD_ALERT") return "is-warning";
        return "is-info";
    }

    function setUnread(next, announce) {
        next = Math.max(0, Number(next) || 0);
        const increased = next > unread;
        unread = next;
        root.setAttribute("data-initial-unread", String(next));
        badge.textContent = String(next);
        badge.hidden = next === 0;
        readAll.disabled = next === 0;
        summary.textContent = next === 0 ? "You're all caught up." : next + " unread";

        if (announce && increased && !reduceMotion.matches) {
            toggle.classList.remove("has-new");
            void toggle.offsetWidth;
            toggle.classList.add("has-new");
            window.setTimeout(() => toggle.classList.remove("has-new"), 650);
        }
    }

    function formatTime(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        const diff = Date.now() - date.getTime();
        const minutes = Math.floor(diff / 60000);
        if (minutes < 1) return "Just now";
        if (minutes < 60) return minutes + "m ago";
        const hours = Math.floor(minutes / 60);
        if (hours < 24) return hours + "h ago";
        return date.toLocaleDateString(undefined, { day: "numeric", month: "short" });
    }

    function emptyState(message) {
        const state = document.createElement("div");
        state.className = "notification-panel-state";
        const strong = document.createElement("strong");
        strong.textContent = message;
        const text = document.createElement("span");
        text.className = "t-micro";
        text.textContent = "Report decisions and important updates will appear here.";
        state.append(strong, text);
        list.replaceChildren(state);
    }

    function render(items) {
        if (!Array.isArray(items) || items.length === 0) {
            emptyState("You're all caught up.");
            return;
        }

        const fragment = document.createDocumentFragment();
        items.forEach(function (item) {
            const link = document.createElement("a");
            link.className = "notification-panel-item " + (item.isRead ? "is-read" : "is-unread");
            link.href = item.actionUrl || "/Notifications";
            link.dataset.notificationLink = "";
            link.dataset.notificationId = String(item.notificationId);
            link.dataset.notificationRead = item.isRead ? "1" : "0";

            const icon = document.createElement("span");
            icon.className = "notification-item-icon " + typeClass(item.typeCode);
            icon.setAttribute("aria-hidden", "true");

            const copy = document.createElement("span");
            copy.className = "notification-panel-copy";
            const title = document.createElement("strong");
            title.textContent = item.title;
            const message = document.createElement("p");
            message.textContent = item.message;
            const time = document.createElement("time");
            time.dateTime = item.createdAt;
            time.textContent = formatTime(item.createdAt);
            copy.append(title, message, time);

            const marker = document.createElement("span");
            if (!item.isRead) {
                marker.className = "notification-unread-dot";
                marker.setAttribute("aria-label", "Unread");
            }

            link.append(icon, copy, marker);
            fragment.appendChild(link);
        });
        list.replaceChildren(fragment);
    }

    async function loadLatest() {
        try {
            const response = await fetch("/api/v1/notifications/latest?take=6", {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) throw new Error("notifications unavailable");
            const data = await response.json();
            render(data.notifications || []);
        } catch (_) {
            emptyState("We couldn't load your notifications.");
        }
    }

    async function refreshUnread(announce) {
        if (document.hidden) return;
        try {
            const response = await fetch("/api/v1/notifications/unread-count", {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) return;
            const data = await response.json();
            setUnread(data.unreadCount, announce);
        } catch (_) {
            // Count polling is noncritical; keep the server-rendered count.
        }
    }

    function openPanel() {
        window.clearTimeout(closeTimer);
        panel.hidden = false;
        requestAnimationFrame(() => panel.classList.add("is-open"));
        toggle.setAttribute("aria-expanded", "true");
        loadLatest();
    }

    function closePanel() {
        if (panel.hidden) return;
        panel.classList.remove("is-open");
        toggle.setAttribute("aria-expanded", "false");
        closeTimer = window.setTimeout(() => { panel.hidden = true; }, reduceMotion.matches ? 0 : 200);
    }

    async function post(url) {
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "X-CSRF-TOKEN": token
            }
        });
    }

    async function markLinkRead(link) {
        if (!link || link.dataset.notificationRead === "1") return true;
        const id = link.dataset.notificationId;
        if (!id) return true;
        try {
            const response = await post("/api/v1/notifications/" + encodeURIComponent(id) + "/read");
            if (!response.ok) return false;
            link.dataset.notificationRead = "1";
            link.classList.remove("is-unread");
            link.classList.add("is-read");
            const dot = link.querySelector(".notification-unread-dot");
            if (dot) dot.remove();
            setUnread(Math.max(0, unread - 1), false);
            return true;
        } catch (_) {
            return false;
        }
    }

    toggle.addEventListener("click", function () {
        if (panel.hidden) openPanel(); else closePanel();
    });
    closeButton.addEventListener("click", closePanel);

    document.addEventListener("click", async function (event) {
        const link = event.target.closest("[data-notification-link]");
        if (link) {
            if (link.dataset.notificationRead !== "1") {
                event.preventDefault();
                const href = link.getAttribute("href") || "/Notifications";
                await markLinkRead(link);
                window.location.assign(href);
            }
            return;
        }

        if (!panel.hidden && !root.contains(event.target)) closePanel();
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !panel.hidden) {
            closePanel();
            toggle.focus();
        }
    });

    readAll.addEventListener("click", async function () {
        if (readAll.disabled) return;
        readAll.disabled = true;
        try {
            const response = await post("/api/v1/notifications/read-all");
            if (!response.ok) throw new Error("read all failed");
            setUnread(0, false);
            await loadLatest();
            window.SafePathToast?.success("All notifications marked as read.");
        } catch (_) {
            readAll.disabled = unread === 0;
            window.SafePathToast?.error("We couldn't update your notifications.");
        }
    });

    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) refreshUnread(true);
    });

    // Updated automatically, not push/real-time. The timer does nothing while the tab is hidden.
    window.setInterval(function () { refreshUnread(true); }, 60000);
})();
