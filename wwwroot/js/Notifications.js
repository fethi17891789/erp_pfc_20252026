/**
 * Notifications.js - Toasts iPhone-style + tiroir de notifications enrichi
 * Chargé globalement via _Layout.cshtml
 */
(function () {
    var unreadCount = 0;

    // --- État enregistrement vocal (un seul à la fois) ---
    var drawerRecorder       = null;
    var drawerRecordedChunks = [];
    var drawerIsRecording    = false;
    var drawerImageTarget    = null; // { convId, item, replyZone, replyBtn }

    // ═══════════════════════════════════════════════════
    // TEMPS RELATIF
    // ═══════════════════════════════════════════════════
    function timeAgo(ts) {
        var diff = Math.floor((Date.now() - ts) / 1000);
        if (diff < 10)  return "À l'instant";
        if (diff < 60)  return "Il y a " + diff + " s";
        var mins = Math.floor(diff / 60);
        if (mins < 60)  return "Il y a " + mins + " min";
        var hrs = Math.floor(mins / 60);
        if (hrs < 24)   return "Il y a " + hrs + "h";
        return "Il y a " + Math.floor(hrs / 24) + "j";
    }

    function refreshAllTimes() {
        var list = document.getElementById("notif-list");
        if (!list) return;
        list.querySelectorAll(".notif-item[data-ts]").forEach(function (item) {
            var timeEl = item.querySelector(".notif-item-time");
            if (timeEl) timeEl.textContent = timeAgo(parseInt(item.dataset.ts, 10));
        });
    }
    setInterval(refreshAllTimes, 30000);

    // ═══════════════════════════════════════════════════
    // TIROIR — ouverture / fermeture animée
    // ═══════════════════════════════════════════════════
    function closeDrawer(drawer) {
        if (!drawer || !drawer.classList.contains("open")) return;
        drawer.classList.remove("open");
        drawer.classList.add("closing");
        setTimeout(function () { drawer.classList.remove("closing"); }, 260);
    }

    window.toggleNotifDrawer = function () {
        var drawer = document.getElementById("notif-drawer");
        if (!drawer) return;
        drawer.classList.contains("open") ? closeDrawer(drawer) : drawer.classList.add("open");
    };

    window.clearAllNotifications = function () {
        unreadCount = 0;
        updateBadge();
        var list = document.getElementById("notif-list");
        if (list) list.innerHTML = '<div class="notif-empty">Aucune notification</div>';
        closeDrawer(document.getElementById("notif-drawer"));
    };

    document.addEventListener("click", function (e) {
        var btn    = document.getElementById("header-notification-btn");
        var drawer = document.getElementById("notif-drawer");
        if (drawer && drawer.classList.contains("open")) {
            if (!drawer.contains(e.target) && btn && !btn.contains(e.target))
                closeDrawer(drawer);
        }
    });

    // ═══════════════════════════════════════════════════
    // BADGE
    // ═══════════════════════════════════════════════════
    function updateBadge() {
        var badge = document.getElementById("notif-badge");
        if (!badge) return;
        badge.textContent = unreadCount > 9 ? "9+" : unreadCount;
        badge.classList.toggle("active", unreadCount > 0);
    }

    // ═══════════════════════════════════════════════════
    // MINI-TOAST IPHONE
    // ═══════════════════════════════════════════════════
    window.showNotificationToast = function (data) {
        var miniToast = document.getElementById("iphone-mini-toast");
        if (!miniToast) return;

        unreadCount++;
        updateBadge();
        addToDrawer(data);

        var senderName = data.SenderName || data.senderName || "Inconnu";
        var content    = data.Content    || data.content    || "Nouveau message";
        var senderId   = data.SenderId   || data.senderId;
        var msgType    = data.MessageType || data.messageType || "text";
        var initial    = senderName.charAt(0).toUpperCase();

        var displayContent = msgType === "audio" ? "🎤 Message vocal"
                           : msgType === "image"  ? "🖼 Image"
                           : content;

        var now = Date.now();
        miniToast.innerHTML =
            '<div class="toast-avatar">' + initial + '</div>' +
            '<div class="toast-content">' +
            '  <div class="toast-title"><span>' + senderName + '</span>' +
            '    <span class="toast-time">' + timeAgo(now) + '</span></div>' +
            '  <div class="toast-msg">' + displayContent + '</div>' +
            '</div>';

        miniToast.onclick = senderId
            ? function () { window.location.href = "/Messagerie?userId=" + senderId; }
            : null;

        miniToast.classList.remove("closing", "open");
        void miniToast.offsetWidth;
        miniToast.classList.add("open");

        if (miniToast.closeTimeout) clearTimeout(miniToast.closeTimeout);
        miniToast.closeTimeout = setTimeout(function () {
            miniToast.classList.remove("open");
            miniToast.classList.add("closing");
            setTimeout(function () { miniToast.classList.remove("closing"); }, 500);
        }, 3000);
    };

    // ═══════════════════════════════════════════════════
    // LECTEUR AUDIO CUSTOM (même design que la messagerie)
    // ═══════════════════════════════════════════════════
    function createNotifAudioPlayer(url) {
        var container = document.createElement("div");
        container.className = "audio-player";

        var playBtn = document.createElement("button");
        playBtn.type = "button";
        playBtn.className = "audio-play-btn";
        playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';

        var rightCol = document.createElement("div");
        rightCol.className = "audio-content-right";

        var visualRow = document.createElement("div");
        visualRow.className = "audio-visual-row";

        var progressContainer = document.createElement("div");
        progressContainer.className = "audio-progress-container";
        var progressBar = document.createElement("div");
        progressBar.className = "audio-progress-bar";
        progressContainer.appendChild(progressBar);

        var waves = document.createElement("div");
        waves.className = "audio-waves";
        for (var i = 0; i < 12; i++) {
            var bar = document.createElement("div");
            bar.className = "audio-wave-bar";
            bar.style.height = (4 + Math.random() * 10) + "px";
            waves.appendChild(bar);
        }
        visualRow.appendChild(progressContainer);
        visualRow.appendChild(waves);

        var infoRow = document.createElement("div");
        infoRow.className = "audio-info-row";
        var titleLabel = document.createElement("span");
        titleLabel.textContent = "Vocal";
        var timeLabel = document.createElement("span");
        timeLabel.textContent = "00:00";
        infoRow.appendChild(titleLabel);
        infoRow.appendChild(timeLabel);

        rightCol.appendChild(visualRow);
        rightCol.appendChild(infoRow);
        container.appendChild(playBtn);
        container.appendChild(rightCol);

        var audio = new Audio(url);

        playBtn.onclick = function (e) {
            e.stopPropagation();
            if (audio.paused) {
                audio.play();
                playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/></svg>';
            } else {
                audio.pause();
                playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
            }
        };
        audio.ontimeupdate = function () {
            var pct = (audio.currentTime / audio.duration) * 100;
            progressBar.style.width = pct + "%";
            var m = Math.floor(audio.currentTime / 60);
            var s = Math.floor(audio.currentTime % 60);
            timeLabel.textContent = (m < 10 ? "0" + m : m) + ":" + (s < 10 ? "0" + s : s);
        };
        audio.onended = function () {
            playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
            progressBar.style.width = "0%";
        };
        progressContainer.onclick = function (e) {
            e.stopPropagation();
            var rect = progressContainer.getBoundingClientRect();
            var pct = (e.clientX - rect.left) / rect.width;
            if (!isNaN(audio.duration)) audio.currentTime = pct * audio.duration;
        };

        return container;
    }

    // ═══════════════════════════════════════════════════
    // TIROIR — ajout d'un item enrichi
    // ═══════════════════════════════════════════════════
    function addToDrawer(data) {
        var list = document.getElementById("notif-list");
        if (!list) return;

        var empty = list.querySelector(".notif-empty");
        if (empty) empty.remove();

        var senderName = data.SenderName   || data.senderName   || "Inconnu";
        var content    = data.Content      || data.content      || "Nouveau message";
        var senderId   = data.SenderId     || data.senderId     || 0;
        var convId     = data.ConversationId || data.conversationId || 0;
        var msgType    = data.MessageType  || data.messageType  || "text";
        var attachUrl  = data.AttachmentUrl || data.attachmentUrl || "";
        var initial    = senderName.charAt(0).toUpperCase();
        var ts         = Date.now();

        // Contenu principal selon le type
        var mainContent;
        var isAudio = (msgType === "audio" && attachUrl);
        if (isAudio) {
            mainContent = '<div class="notif-audio-placeholder"></div>';
        } else if (msgType === "image" && attachUrl) {
            mainContent = '<img class="notif-img-preview" src="' + attachUrl + '" alt="Image">';
        } else {
            mainContent = '<div class="notif-item-text">' + content + '</div>';
        }

        // Zone de reply (uniquement si convId connu)
        var replyHtml = convId > 0 ? (
            '<div class="notif-reply-zone">' +
            '  <div class="notif-reply-row">' +
            '    <input class="notif-reply-input" type="text" placeholder="Répondre à ' + senderName + '\u2026">' +
            '    <button class="notif-reply-send" type="button" title="Envoyer">' +
            '      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">' +
            '        <line x1="22" y1="2" x2="11" y2="13"/>' +
            '        <polygon points="22 2 15 22 11 13 2 9 22 2"/>' +
            '      </svg>' +
            '    </button>' +
            '    <button class="notif-reply-mic" type="button" title="Message vocal">' +
            '      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">' +
            '        <rect x="9" y="2" width="6" height="12" rx="3"/>' +
            '        <path d="M5 10a7 7 0 0 0 14 0"/>' +
            '        <line x1="12" y1="19" x2="12" y2="22"/>' +
            '        <line x1="8" y1="22" x2="16" y2="22"/>' +
            '      </svg>' +
            '    </button>' +
            '    <button class="notif-reply-img" type="button" title="Envoyer une image">' +
            '      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">' +
            '        <rect x="3" y="3" width="18" height="18" rx="2"/>' +
            '        <circle cx="8.5" cy="8.5" r="1.5"/>' +
            '        <polyline points="21 15 16 10 5 21"/>' +
            '      </svg>' +
            '    </button>' +
            '  </div>' +
            '  <div class="notif-rec-zone">' +
            '    <span class="notif-rec-dot"></span>' +
            '    <span class="notif-rec-label">Enregistrement\u2026</span>' +
            '    <button class="notif-rec-stop" type="button">\u25a0 Envoyer</button>' +
            '  </div>' +
            '</div>'
        ) : '';

        var item = document.createElement("div");
        item.className     = "notif-item";
        item.dataset.ts    = ts;
        item.dataset.convId   = convId;
        item.dataset.senderId = senderId;
        item.innerHTML =
            '<div class="notif-item-main">' +
            '  <div class="notif-item-avatar">' + initial + '</div>' +
            '  <div class="notif-item-body">' +
            '    <div class="notif-item-title">' + senderName + '</div>' +
            '    ' + mainContent +
            '    <div class="notif-item-footer">' +
            '      <span class="notif-item-time">' + timeAgo(ts) + '</span>' +
            (convId > 0 ? '<button class="notif-reply-btn" type="button">Répondre</button>' : '') +
            '    </div>' +
            '  </div>' +
            '</div>' +
            replyHtml;

        // Injection du lecteur audio custom (après innerHTML)
        if (isAudio) {
            var placeholder = item.querySelector(".notif-audio-placeholder");
            if (placeholder) placeholder.replaceWith(createNotifAudioPlayer(attachUrl));
        }

        // --- Événements ---
        if (convId > 0) {
            var replyBtn  = item.querySelector(".notif-reply-btn");
            var replyZone = item.querySelector(".notif-reply-zone");
            var replyInput = item.querySelector(".notif-reply-input");
            var replySend  = item.querySelector(".notif-reply-send");
            var micBtn     = item.querySelector(".notif-reply-mic");
            var imgBtn     = item.querySelector(".notif-reply-img");
            var recZone    = item.querySelector(".notif-rec-zone");
            var recStop    = item.querySelector(".notif-rec-stop");
            var replyRow   = item.querySelector(".notif-reply-row");

            // Toggle zone reply
            replyBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                var open = replyZone.classList.toggle("open");
                replyBtn.textContent = open ? "Fermer" : "Répondre";
                if (open && replyInput) replyInput.focus();
            });

            // Envoi texte
            function sendText() {
                var text = replyInput ? replyInput.value.trim() : "";
                if (!text) return;
                replyInput.value = "";
                sendDrawerMessage(convId, { content: text, messageType: "text", attachmentUrl: null });
                replyZone.classList.remove("open");
                replyBtn.textContent = "Répondre";
                window.showErpToast("Message envoyé.", "success");
            }
            if (replySend)  replySend.addEventListener("click",  function (e) { e.stopPropagation(); sendText(); });
            if (replyInput) replyInput.addEventListener("keyup", function (e) { if (e.key === "Enter") sendText(); });

            // Enregistrement vocal
            if (micBtn) micBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                startDrawerRecording(item, convId, replyRow, recZone);
            });
            if (recStop) recStop.addEventListener("click", function (e) {
                e.stopPropagation();
                stopDrawerRecording(replyRow, recZone);
                afterSend(replyZone, replyBtn);
            });

            // Image (bouton)
            if (imgBtn) imgBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                drawerImageTarget = { convId: convId, replyZone: replyZone, replyBtn: replyBtn };
                var inp = document.getElementById("drawer-img-input");
                if (inp) inp.click();
            });

            // Drag & drop image
            item.addEventListener("dragover", function (e) {
                if (e.dataTransfer && e.dataTransfer.types && Array.prototype.indexOf.call(e.dataTransfer.types, "Files") !== -1) {
                    e.preventDefault();
                    e.stopPropagation();
                    item.classList.add("notif-drop-active");
                }
            });
            item.addEventListener("dragleave", function (e) {
                if (!item.contains(e.relatedTarget)) {
                    item.classList.remove("notif-drop-active");
                }
            });
            item.addEventListener("drop", function (e) {
                e.preventDefault();
                e.stopPropagation();
                item.classList.remove("notif-drop-active");
                var files = e.dataTransfer && e.dataTransfer.files;
                if (!files || files.length === 0) return;
                var file = files[0];
                if (!file.type.startsWith("image/")) {
                    window.showErpToast("Seules les images sont acceptées.", "warning");
                    return;
                }
                uploadDrawerImage(file, { convId: convId, replyZone: replyZone, replyBtn: replyBtn });
            });
        }

        // Clic sur l'item → aller à la conversation (sauf dans la reply zone)
        item.addEventListener("click", function (e) {
            if (e.target.closest(".notif-reply-zone") || e.target.closest(".notif-reply-btn")) return;
            if (senderId > 0) window.location.href = "/Messagerie?userId=" + senderId;
        });

        list.insertBefore(item, list.firstChild);
        while (list.children.length > 15) list.removeChild(list.lastChild);
    }

    function afterSend(replyZone, replyBtn) {
        if (replyZone) replyZone.classList.remove("open");
        if (replyBtn)  replyBtn.textContent = "Répondre";
    }

    // ═══════════════════════════════════════════════════
    // ENVOI VIA SIGNALR
    // ═══════════════════════════════════════════════════
    function sendDrawerMessage(convId, payload) {
        var conn = window.presenceConnection;
        if (!conn) { window.showErpToast("Connexion temps réel indisponible.", "error"); return; }
        var dto = {
            conversationId: convId,
            senderId:   window.currentUserIdFromServer   || 0,
            senderName: window.currentUserNameFromServer || "Moi",
            content:       payload.content       || "",
            messageType:   payload.messageType   || "text",
            attachmentUrl: payload.attachmentUrl || null
        };
        var method = payload.messageType === "audio" ? "SendAudioMessage" : "SendMessage";
        conn.invoke(method, dto).catch(function (e) { console.error("[Drawer] " + method + ":", e); });
    }

    // ═══════════════════════════════════════════════════
    // TOKEN CSRF (meta tag global injecté par _Layout.cshtml)
    // ═══════════════════════════════════════════════════
    function getCsrfToken() {
        var meta = document.querySelector("meta[name='csrf-token']");
        if (meta && meta.content) return meta.content;
        // Fallback : input caché dans un formulaire Razor présent sur la page
        var input = document.querySelector("input[name='__RequestVerificationToken']");
        return input ? input.value : null;
    }

    // ═══════════════════════════════════════════════════
    // ENREGISTREMENT VOCAL
    // ═══════════════════════════════════════════════════
    function startDrawerRecording(item, convId, replyRow, recZone) {
        if (drawerIsRecording) { window.showErpToast("Un enregistrement est déjà en cours.", "warning"); return; }
        if (!navigator.mediaDevices) { window.showErpToast("Micro non disponible dans ce navigateur.", "error"); return; }

        navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
            drawerRecordedChunks = [];
            drawerIsRecording    = true;

            if (replyRow) replyRow.style.display = "none";
            if (recZone)  recZone.style.display  = "flex";

            drawerRecorder = new MediaRecorder(stream, { mimeType: "audio/webm" });
            drawerRecorder.ondataavailable = function (e) {
                if (e.data && e.data.size > 0) drawerRecordedChunks.push(e.data);
            };
            drawerRecorder.onstop = function () {
                stream.getTracks().forEach(function (t) { t.stop(); });
                var blob = new Blob(drawerRecordedChunks, { type: "audio/webm" });
                uploadDrawerAudio(blob, convId);
                drawerIsRecording = false;
            };
            drawerRecorder.start();
        }).catch(function () {
            window.showErpToast("Impossible d'accéder au micro.", "error");
        });
    }

    function stopDrawerRecording(replyRow, recZone) {
        if (!drawerIsRecording || !drawerRecorder) return;
        drawerRecorder.stop();
        if (recZone)  recZone.style.display  = "none";
        if (replyRow) replyRow.style.display = "flex";
    }

    async function uploadDrawerAudio(blob, convId) {
        var csrfToken = getCsrfToken();
        if (!csrfToken) { window.showErpToast("Token CSRF manquant.", "error"); return; }

        var formData = new FormData();
        formData.append("conversationId", convId);
        formData.append("audioFile", blob, "audio.webm");
        formData.append("__RequestVerificationToken", csrfToken);

        try {
            var resp = await fetch("/Messagerie?handler=UploadAudio", { method: "POST", body: formData });
            if (!resp.ok) throw new Error("HTTP " + resp.status);
            var msg = await resp.json();
            sendDrawerMessage(convId, {
                content:       msg.Content       || "[message audio]",
                messageType:   "audio",
                attachmentUrl: msg.AttachmentUrl || msg.attachmentUrl || null
            });
            window.showErpToast("Message vocal envoyé.", "success");
        } catch (e) {
            console.error("[Drawer] uploadDrawerAudio:", e);
            window.showErpToast("Erreur lors de l'envoi du message vocal.", "error");
        }
    }

    // ═══════════════════════════════════════════════════
    // ENVOI D'IMAGE
    // ═══════════════════════════════════════════════════
    document.addEventListener("DOMContentLoaded", function () {
        var imgInput = document.getElementById("drawer-img-input");
        if (!imgInput) return;
        imgInput.addEventListener("change", function (e) {
            var file = e.target.files[0];
            e.target.value = "";
            if (!file || !drawerImageTarget) return;
            var target = drawerImageTarget;
            drawerImageTarget = null;
            uploadDrawerImage(file, target);
        });
    });

    async function uploadDrawerImage(file, target) {
        var csrfToken = getCsrfToken();
        if (!csrfToken) { window.showErpToast("Token CSRF manquant.", "error"); return; }

        var formData = new FormData();
        formData.append("conversationId", target.convId);
        formData.append("chatFile", file);
        formData.append("__RequestVerificationToken", csrfToken);

        try {
            var resp = await fetch("/Messagerie?handler=UploadFile", { method: "POST", body: formData });
            if (!resp.ok) throw new Error("HTTP " + resp.status);
            var msg = await resp.json();
            sendDrawerMessage(target.convId, {
                content:       msg.Content      || file.name,
                messageType:   msg.MessageType  || msg.messageType || "image",
                attachmentUrl: msg.AttachmentUrl || msg.attachmentUrl || null
            });
            afterSend(target.replyZone, target.replyBtn);
            window.showErpToast("Image envoyée.", "success");
        } catch (e) {
            console.error("[Drawer] uploadDrawerImage:", e);
            window.showErpToast("Erreur lors de l'envoi de l'image.", "error");
        }
    }

    // ═══════════════════════════════════════════════════
    // NOTIFICATION SYSTÈME (blockchain, OF, OA…)
    // ═══════════════════════════════════════════════════
    window.showSystemNotification = function (titre, message) {
        window.showNotificationToast({
            SenderName: titre,
            Content: message,
            SenderId: null
        });
    };

    // ═══════════════════════════════════════════════════
    // TOAST GÉNÉRIQUE (success / error / info / warning)
    // ═══════════════════════════════════════════════════
    window.showErpToast = function (message, type) {
        var t = document.createElement('div');
        var colors = {
            success: { bg: 'linear-gradient(135deg,#7B5EFF,#5b3fd4)', shadow: 'rgba(123,94,255,0.35)' },
            error:   { bg: 'linear-gradient(135deg,#e85d5d,#b33b3b)', shadow: 'rgba(232,93,93,0.35)' },
            info:    { bg: 'linear-gradient(135deg,#38bdf8,#0ea5e9)',  shadow: 'rgba(56,189,248,0.35)' },
            warning: { bg: 'linear-gradient(135deg,#c97a1a,#a35d0d)', shadow: 'rgba(201,122,26,0.35)' }
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
        }, (type === 'error' || type === 'warning') ? 4000 : 2800);
    };
})();
