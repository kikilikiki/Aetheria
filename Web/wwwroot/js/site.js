(function () {
    // --- Statut du serveur de jeu ---
    var dot = document.getElementById("server-dot");
    var label = document.getElementById("server-status");
    var healthUrl = document.body.getAttribute("data-health-url");

    if (dot && label && healthUrl) {
        fetch(healthUrl, { cache: "no-store" })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
            .then(function (data) {
                dot.classList.add("online");
                label.textContent = "Serveur en ligne" + (data && data.version ? " — v" + data.version : "");
            })
            .catch(function () {
                dot.classList.add("offline");
                label.textContent = "Serveur hors ligne";
            });
    } else if (label) {
        label.textContent = "";
    }

    // --- Sélecteur d'OS (page Télécharger) ---
    window.selectOs = function (os) {
        document.querySelectorAll(".os-tab").forEach(function (tab) {
            tab.classList.toggle("active", tab.dataset.os === os);
        });
        document.querySelectorAll(".os-panel").forEach(function (panel) {
            panel.classList.toggle("active", panel.id === "os-panel-" + os);
        });
        try { localStorage.setItem("aetheria-os", os); } catch (e) { /* ignore */ }
    };

    if (document.querySelector(".os-tabs")) {
        var saved = null;
        try { saved = localStorage.getItem("aetheria-os"); } catch (e) { /* ignore */ }
        var ua = (navigator.userAgent || "").toLowerCase();
        var detected = (ua.indexOf("linux") !== -1 && ua.indexOf("android") === -1) ? "linux" : "windows";
        window.selectOs(saved === "linux" || saved === "windows" ? saved : detected);
    }
})();
