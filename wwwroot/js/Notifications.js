/**
 * Notifications.js - Toasts iPhone-style + tiroir de notifications
 * Chargé globalement via _Layout.cshtml
 */
(function () {
    var unreadCount = 0;

    // --- Toggle du tiroir ---
    window.toggleNotifDrawer = function () {
        var drawer = document.getElementById("notif-drawer");
        if (drawer) drawer.classList.toggle("open");
    };

    // --- Tout effacer ---
    window.clearAllNotifications = function () {
        unreadCount = 0;
        updateBadge();
        var list = document.getElementById("notif-list");
        if (list) list.innerHTML = '<div class="notif-empty">Aucune notification</div>';
        var drawer = document.getElementById("notif-drawer");
        if (drawer) drawer.classList.remove("open");
    };

    // --- Fermer le tiroir si clic ailleurs ---
    document.addEventListener("click", function (e) {
        var btn = document.getElementById("header-notification-btn");
        var drawer = document.getElementById("notif-drawer");
        if (drawer && drawer.classList.contains("open")) {
            if (!drawer.contains(e.target) && btn && !btn.contains(e.target)) {
                drawer.classList.remove("open");
            }
        }
    });

    // --- Badge ---
    function updateBadge() {
        var badge = document.getElementById("notif-badge");
        if (!badge) return;
        badge.textContent = unreadCount > 9 ? "9+" : unreadCount;
        if (unreadCount > 0) {
            badge.classList.add("active");
        } else {
            badge.classList.remove("active");
        }
    }

    // --- Afficher un mini-toast iPhone depuis la cloche ---
    window.showNotificationToast = function (data) {
        var miniToast = document.getElementById("iphone-mini-toast");
        if (!miniToast) return;

        unreadCount++;
        updateBadge();
        addToDrawer(data);

        var senderName = data.SenderName || data.senderName || "Inconnu";
        var content = data.Content || data.content || "Nouveau message";
        var senderId = data.SenderId || data.senderId;
        var initial = senderName.charAt(0).toUpperCase();

        miniToast.innerHTML =
            '<div class="toast-avatar">' + initial + '</div>' +
            '<div class="toast-content">' +
            '  <div class="toast-title"><span>' + senderName + '</span><span class="toast-time">Maintenant</span></div>' +
            '  <div class="toast-msg">' + content + '</div>' +
            '</div>';

        miniToast.onclick = function () {
            if (senderId) window.location.href = "/Messagerie?userId=" + senderId;
        };

        // Reset state
        miniToast.classList.remove("closing");
        miniToast.classList.remove("open");
        
        // Force reflow
        void miniToast.offsetWidth;

        // Animate in
        miniToast.classList.add("open");

        // Clear previous timeout if exists
        if (miniToast.closeTimeout) {
            clearTimeout(miniToast.closeTimeout);
        }

        // Auto-dismiss après 3s avec animation bouncy de sortie
        miniToast.closeTimeout = setTimeout(function () {
            miniToast.classList.remove("open");
            miniToast.classList.add("closing");
            setTimeout(function () {
                miniToast.classList.remove("closing");
            }, 500); // 500ms duration of miniDrawerUp
        }, 3000);
    };

    // --- Ajouter au tiroir ---
    function addToDrawer(data) {
        var list = document.getElementById("notif-list");
        if (!list) return;

        var empty = list.querySelector(".notif-empty");
        if (empty) empty.remove();

        var senderName = data.SenderName || data.senderName || "Inconnu";
        var content = data.Content || data.content || "Nouveau message";
        var senderId = data.SenderId || data.senderId;
        var initial = senderName.charAt(0).toUpperCase();

        var item = document.createElement("div");
        item.className = "notif-item";
        item.innerHTML =
            '<div class="notif-item-avatar">' + initial + '</div>' +
            '<div class="notif-item-body">' +
            '  <div class="notif-item-title">' + senderName + '</div>' +
            '  <div class="notif-item-text">' + content + '</div>' +
            '  <div class="notif-item-time">À l\'instant</div>' +
            '</div>';

        item.onclick = function () {
            if (senderId) window.location.href = "/Messagerie?userId=" + senderId;
        };

        list.insertBefore(item, list.firstChild);

        // Max 15 items
        while (list.children.length > 15) {
            list.removeChild(list.lastChild);
        }
    }

    // --- Notification système (OF, OA blockchain, etc.) sans redirection ---
    window.showSystemNotification = function (titre, message) {
        window.showNotificationToast({
            SenderName: titre,
            Content: message,
            SenderId: null
        });
        // Surcharger onclick pour ne pas rediriger
        var miniToast = document.getElementById("iphone-mini-toast");
        if (miniToast) miniToast.onclick = null;
    };

    // --- Toast générique succès/erreur/info utilisable partout ---
    window.showErpToast = function (message, type) {
        var t = document.createElement('div');
        var colors = {
            success: { bg: 'linear-gradient(135deg,#7B5EFF,#5b3fd4)', shadow: 'rgba(123,94,255,0.35)' },
            error:   { bg: 'linear-gradient(135deg,#e85d5d,#b33b3b)', shadow: 'rgba(232,93,93,0.35)' },
            info:    { bg: 'linear-gradient(135deg,#38bdf8,#0ea5e9)',  shadow: 'rgba(56,189,248,0.35)' }
        };
        var c = colors[type] || colors.info;
        t.style.cssText = [
            'position:fixed', 'bottom:32px', 'left:50%',
            'transform:translateX(-50%) translateY(20px)',
            'padding:12px 24px', 'border-radius:12px',
            'font-size:0.9rem', 'font-weight:600', 'color:#fff',
            'opacity:0', 'pointer-events:none', 'z-index:99999',
            'transition:opacity .3s ease,transform .3s ease',
            'white-space:nowrap', 'font-family:inherit',
            'background:' + c.bg,
            'box-shadow:0 8px 24px ' + c.shadow
        ].join(';');
        t.textContent = message;
        document.body.appendChild(t);
        requestAnimationFrame(function () {
            t.style.opacity = '1';
            t.style.transform = 'translateX(-50%) translateY(0)';
        });
        setTimeout(function () {
            t.style.opacity = '0';
            t.style.transform = 'translateX(-50%) translateY(20px)';
            setTimeout(function () { t.remove(); }, 350);
        }, type === 'error' ? 4000 : 2800);
    };
})();
