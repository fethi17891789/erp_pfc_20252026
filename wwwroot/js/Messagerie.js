// Fichier : wwwroot/js/messagerie.js

// ID utilisateur courant récupéré depuis la page Razor
let currentUserId = window.currentUserIdFromServer || 0;
let currentUserName = "Vous";

let currentTargetUserId = null;
let currentTargetUserName = null;
let currentConversationId = null;

// Variables pour l'audio
let mediaRecorder = null;
let recordedChunks = [];
let isRecording = false;

// Connexion SignalR (on passe le userId dans l'URL)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub?userId=" + encodeURIComponent(currentUserId))
    .withAutomaticReconnect()
    .build();

// ====================== GESTION STATUT EN LIGNE / HORS LIGNE ======================

// Mise à jour du badge dans la liste des utilisateurs
function updateUserItemOnlineStatus(userId, isOnline) {
    const container = document.getElementById("usersContainer");
    if (!container) return;

    const items = container.querySelectorAll(".user-item");
    items.forEach(item => {
        const idStr = item.getAttribute("data-user-id");
        if (!idStr) return;

        const id = parseInt(idStr, 10);
        if (id !== userId) return;

        // Mettre à jour l'attribut data-isonline (utile pour la sélection / header)
        item.setAttribute("data-isonline", isOnline ? "true" : "false");

        const dot = item.querySelector(".chat-user-status");
        const text = item.querySelector(".chat-user-status-text");

        if (dot) {
            dot.classList.remove("online", "offline");
            dot.classList.add(isOnline ? "online" : "offline");
        }

        if (text) {
            text.classList.remove("online", "offline");
            text.classList.add(isOnline ? "online" : "offline");
            text.textContent = isOnline ? "En ligne" : "Hors ligne";
        }
    });
}

// Mise à jour du header (utilisateur sélectionné)
function updateHeaderOnlineStatus(isOnline) {
    const statusDot = document.getElementById("chatHeaderStatusDot");
    const statusText = document.getElementById("chatHeaderStatusText");

    if (statusDot) {
        statusDot.classList.remove("online", "offline");
        statusDot.classList.add(isOnline ? "online" : "offline");
    }

    if (statusText) {
        statusText.textContent = isOnline ? "En ligne" : "Hors ligne";
    }
}

// ====================== HANDLERS SIGNALR ======================

// Réception d'un message temps réel
connection.on("ReceiveMessage", function (message) {
    if (!message || (!message.conversationId && !message.ConversationId)) return;

    const convId = message.conversationId || message.ConversationId;
    if (convId !== currentConversationId) return;

    // Par défaut, un nouveau message vient d'être envoyé => pas encore lu par l'autre
    if (typeof message.isReadByOther === "undefined" && typeof message.IsReadByOther === "undefined") {
        message.isReadByOther = false;
    }

    appendMessageToUi(message);

    // Si le message vient de l'autre utilisateur, on le marque comme lu (coté serveur)
    const senderId = message.senderId || message.SenderId;
    if (senderId && senderId !== currentUserId) {
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("MarkConversationAsRead", currentConversationId, currentUserId)
                .catch(err => console.warn("Erreur MarkConversationAsRead (ReceiveMessage):", err.toString()));
        }
    }
});

// Notification de conversation lue (read receipts)
connection.on("ConversationRead", function (info) {
    // info = { conversationId, readerUserId }
    if (!info) return;

    const convId = info.conversationId || info.ConversationId;
    const readerUserId = info.readerUserId || info.ReaderUserId;

    // On ne s'intéresse qu'à la conversation actuellement ouverte
    if (!currentConversationId || convId !== currentConversationId) return;

    // Si c'est moi qui lis, je ne change pas mon propre UI
    if (readerUserId === currentUserId) {
        return;
    }

    // Ici : readerUserId = l'autre utilisateur
    // Pour tous MES messages "sent" de cette conversation, on passe "Envoyé" -> "Vu"
    const container = document.getElementById("messagesContainer");
    if (!container) return;

    const wrappers = container.children;
    for (let i = 0; i < wrappers.length; i++) {
        const w = wrappers[i];
        const senderIdStr = w.dataset.senderId;
        const msgStatus = w.dataset.msgStatus;

        if (!senderIdStr) continue;
        const senderId = parseInt(senderIdStr, 10);

        if (senderId === currentUserId && msgStatus === "sent") {
            const statusSpanId = w.dataset.statusSpanId;
            const statusSpan = statusSpanId ? document.getElementById(statusSpanId) : null;
            if (statusSpan) {
                statusSpan.textContent = "Vu";
            }
            w.dataset.msgStatus = "read";
        }
    }
});

// Un utilisateur passe en ligne
connection.on("UserOnline", function (info) {
    if (!info) return;
    const userId = info.userId || info.UserId;
    if (!userId) return;

    updateUserItemOnlineStatus(userId, true);

    // Si c'est l'utilisateur actuellement sélectionné dans le header
    if (currentTargetUserId && userId === currentTargetUserId) {
        updateHeaderOnlineStatus(true);
    }
});

// Un utilisateur passe hors ligne
connection.on("UserOffline", function (info) {
    if (!info) return;
    const userId = info.userId || info.UserId;
    if (!userId) return;

    updateUserItemOnlineStatus(userId, false);

    if (currentTargetUserId && userId === currentTargetUserId) {
        updateHeaderOnlineStatus(false);
    }
});

// Démarrer la connexion
connection.start().then(function () {
    console.log("SignalR connecté");

    // Optionnel : récupérer la liste des users en ligne au cas où
    connection.invoke("GetOnlineUsers")
        .then(function (userIds) {
            if (!Array.isArray(userIds)) return;
            userIds.forEach(id => {
                updateUserItemOnlineStatus(id, true);
            });
        })
        .catch(function (err) {
            console.warn("Erreur GetOnlineUsers:", err.toString());
        });
}).catch(function (err) {
    console.error(err.toString());
});

// ====================== LOGIQUE UI ======================

document.addEventListener("DOMContentLoaded", function () {
    console.log("DOM loaded messagerie.js");

    const input = document.getElementById("chatInput");
    const btnSend = document.getElementById("btnSend");
    const usersContainer = document.getElementById("usersContainer");
    const headerName = document.getElementById("chatTargetName");
    const headerInitial = document.getElementById("chatTargetInitial");
    const headerStatusDot = document.getElementById("chatHeaderStatusDot");
    const headerStatusText = document.getElementById("chatHeaderStatusText");
    const messagesContainer = document.getElementById("messagesContainer");
    const btnRecordAudio = document.getElementById("btnRecordAudio");

    const btnAttachFile = document.getElementById("btnAttachFile");
    const fileInput = document.getElementById("chatFileInput");

    if (!input || !btnSend || !usersContainer || !messagesContainer) {
        console.warn("éléments de base manquants");
        return;
    }

    // Clic sur un utilisateur dans la liste de gauche
    usersContainer.addEventListener("click", async function (e) {
        const btn = e.target.closest(".user-item");
        if (!btn) return;

        const userIdStr = btn.getAttribute("data-user-id");
        const login = btn.getAttribute("data-login") || "";
        const email = btn.getAttribute("data-email") || "";
        const isOnlineAttr = btn.getAttribute("data-isonline");
        const isOnline = isOnlineAttr === "true";

        const displayName = login || email || "Utilisateur";

        if (!userIdStr) return;
        const userId = parseInt(userIdStr, 10);
        if (!userId || isNaN(userId)) return;

        if (userId === currentUserId) {
            alert("Tu ne peux pas discuter avec toi-même.");
            return;
        }

        currentTargetUserId = userId;
        currentTargetUserName = displayName;

        try {
            const url = `/Messagerie?handler=Conversation&otherUserId=${encodeURIComponent(userId)}`;
            const response = await fetch(url, { method: "GET" });
            if (!response.ok) {
                console.error("Erreur HTTP Conversation", response.status);
                return;
            }

            const conv = await response.json();

            // Quitter l'ancienne conversation SignalR
            if (currentConversationId && connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke("LeaveConversation", currentConversationId);
                } catch (err) {
                    console.warn("Erreur LeaveConversation:", err.toString());
                }
            }

            currentConversationId = conv.conversationId;
            console.log("Conversation chargée, currentConversationId =", currentConversationId);

            // Rejoindre la nouvelle conversation
            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke("JoinConversation", currentConversationId);
                } catch (err) {
                    console.error("Erreur JoinConversation:", err.toString());
                }
            }

            // Mettre à jour le header
            const otherName = conv.otherUserName || displayName;
            if (headerName) headerName.textContent = otherName;
            if (headerInitial) {
                const initial = (otherName || "?").charAt(0).toUpperCase();
                headerInitial.textContent = initial;
            }
            // Statut header en fonction de data-isonline
            if (headerStatusDot && headerStatusText) {
                updateHeaderOnlineStatus(isOnline);
            }

            // Afficher l'historique
            messagesContainer.innerHTML = "";
            if (Array.isArray(conv.messages)) {
                conv.messages.forEach(m => appendMessageToUi(m));
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }

            // Marquer la conversation comme lue pour l'utilisateur courant
            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    connection.invoke("MarkConversationAsRead", currentConversationId, currentUserId)
                        .catch(err => console.warn("Erreur MarkConversationAsRead:", err.toString()));
                } catch (err) {
                    console.warn("Erreur MarkConversationAsRead:", err.toString());
                }
            }
        } catch (err) {
            console.error("Erreur lors du chargement de la conversation :", err);
        }
    });

    function send() {
        const content = input.value.trim();
        if (!content) return;

        if (!currentConversationId || !currentTargetUserId) {
            alert("Sélectionne d'abord un destinataire dans la liste à gauche.");
            return;
        }

        const dto = {
            conversationId: currentConversationId,
            senderId: currentUserId,
            senderName: currentUserName,
            content: content,
            messageType: "text",
            attachmentUrl: null
        };

        connection.invoke("SendMessage", dto)
            .then(() => {
                input.value = "";
            })
            .catch(err => console.error(err.toString()));
    }

    btnSend.addEventListener("click", send);
    input.addEventListener("keyup", function (e) {
        if (e.key === "Enter") {
            send();
        }
    });

    // Gestion du bouton d'enregistrement audio
    if (btnRecordAudio) {
        btnRecordAudio.addEventListener("click", toggleRecording);
    }

    // Gestion du bouton trombone + input fichier
    if (btnAttachFile && fileInput) {
        btnAttachFile.addEventListener("click", function () {
            if (!currentConversationId || !currentTargetUserId) {
                alert("Sélectionne d'abord un destinataire dans la liste à gauche.");
                return;
            }
            fileInput.click();
        });

        fileInput.addEventListener("change", handleChatFilesSelected);
    } else {
        console.warn("btnAttachFile ou fileInput introuvable");
    }
});

// ====================== GESTION AUDIO ======================

// Démarrer/arrêter l'enregistrement audio
async function toggleRecording() {
    if (!currentConversationId || !currentTargetUserId) {
        alert("Sélectionne d'abord un destinataire dans la liste à gauche.");
        return;
    }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        alert("Votre navigateur ne supporte pas l'enregistrement audio.");
        return;
    }

    if (!isRecording) {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            recordedChunks = [];
            mediaRecorder = new MediaRecorder(stream, { mimeType: "audio/webm" });

            mediaRecorder.ondataavailable = function (e) {
                if (e.data && e.data.size > 0) {
                    recordedChunks.push(e.data);
                }
            };

            mediaRecorder.onstop = function () {
                const blob = new Blob(recordedChunks, { type: "audio/webm" });
                uploadAudioBlob(blob);
                stream.getTracks().forEach(t => t.stop());
            };

            mediaRecorder.start();
            isRecording = true;
            updateRecordButton(true);
        } catch (err) {
            console.error("Erreur getUserMedia:", err);
            alert("Impossible d'accéder au micro.");
        }
    } else {
        if (mediaRecorder && mediaRecorder.state !== "inactive") {
            mediaRecorder.stop();
        }
        isRecording = false;
        updateRecordButton(false);
    }
}

function updateRecordButton(isRec) {
    const btnRecordAudio = document.getElementById("btnRecordAudio");
    if (!btnRecordAudio) return;

    if (isRec) {
        btnRecordAudio.style.color = "#f97373";
        btnRecordAudio.style.backgroundColor = "rgba(248,113,113,0.12)";
    } else {
        btnRecordAudio.style.color = "var(--text-muted-2)";
        btnRecordAudio.style.backgroundColor = "transparent";
    }
}

// Upload du blob audio vers le handler Razor
async function uploadAudioBlob(blob) {
    if (!blob || blob.size === 0) return;
    if (!currentConversationId) {
        alert("Conversation invalide.");
        return;
    }

    const formData = new FormData();
    formData.append("conversationId", currentConversationId);
    formData.append("audioFile", blob, "audio.webm");

    const tokenInput = document.getElementById("__RequestVerificationToken");
    if (tokenInput) {
        formData.append("__RequestVerificationToken", tokenInput.value);
    }

    try {
        const response = await fetch("/Messagerie?handler=UploadAudio", {
            method: "POST",
            body: formData
        });

        if (!response.ok) {
            const txt = await response.text().catch(() => "");
            console.error("Erreur HTTP UploadAudio:", response.status, txt);
            return;
        }

        const msg = await response.json();

        try {
            await connection.invoke("SendAudioMessage", msg);
        } catch (err) {
            console.warn("Erreur SendAudioMessage:", err.toString());
        }
    } catch (err) {
        console.error("Erreur upload audio:", err);
    }
}

// ====================== GESTION FICHIERS ======================

function handleChatFilesSelected(e) {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    if (!currentConversationId || !currentTargetUserId) {
        alert("Sélectionne d'abord un destinataire dans la liste à gauche.");
        e.target.value = "";
        return;
    }

    const tokenInput = document.getElementById("__RequestVerificationToken");
    const token = tokenInput ? tokenInput.value : "";

    Array.from(files).forEach(file => {
        const formData = new FormData();
        formData.append("conversationId", currentConversationId);
        formData.append("chatFile", file);
        formData.append("__RequestVerificationToken", token);

        fetch("/Messagerie?handler=UploadFile", {
            method: "POST",
            body: formData
        })
            .then(r => {
                if (!r.ok) throw new Error("Erreur upload fichier");
                return r.json();
            })
            .then(messageDto => {
                connection.invoke("SendMessage", messageDto)
                    .catch(err => console.error("Erreur SendMessage fichier:", err.toString()));
            })
            .catch(err => console.error("Erreur upload fichier:", err));
    });

    e.target.value = "";
}

// ====================== UTILITAIRES MESSAGES ======================

// Utilitaire format date -> "dd/MM HH:mm"
function formatTimestampForDisplay(timestampValue) {
    if (!timestampValue) return "";

    const d = new Date(timestampValue);
    if (isNaN(d.getTime())) return "";

    const day = d.getDate().toString().padStart(2, "0");
    const month = (d.getMonth() + 1).toString().padStart(2, "0");
    const hours = d.getHours().toString().padStart(2, "0");
    const minutes = d.getMinutes().toString().padStart(2, "0");

    return `${day}/${month} ${hours}:${minutes}`;
}

// OUVERTURE / FERMETURE OVERLAY IMAGE
function openImageOverlay(imageUrl) {
    const overlay = document.getElementById("chatImageOverlay");
    if (!overlay) return;

    const imgEl = document.getElementById("chatImageOverlayImg");
    if (imgEl) {
        imgEl.src = imageUrl;
    }

    overlay.style.display = "flex";
}

function closeImageOverlay() {
    const overlay = document.getElementById("chatImageOverlay");
    if (!overlay) return;

    overlay.style.display = "none";
}

// Fermer l'overlay au clic sur le fond
document.addEventListener("click", function (e) {
    const overlay = document.getElementById("chatImageOverlay");
    if (!overlay) return;
    if (e.target === overlay) {
        closeImageOverlay();
    }
});

// Utilitaire pour afficher un message
function appendMessageToUi(message) {
    const container = document.getElementById("messagesContainer");
    if (!container) return;

    const senderId = message.senderId || message.SenderId;
    const content = message.content || message.Content || "";
    const messageType = message.messageType || message.MessageType || "text";
    const attachmentUrl = message.attachmentUrl || message.AttachmentUrl || null;
    const timestampRaw = message.timestamp || message.Timestamp || null;
    const timestampText = formatTimestampForDisplay(timestampRaw);

    // flag de lecture calculé par le backend (persistance)
    const isReadByOther =
        (typeof message.isReadByOther !== "undefined" ? message.isReadByOther : message.IsReadByOther) || false;

    const isMe = senderId === currentUserId;

    const wrapper = document.createElement("div");
    wrapper.style.display = "flex";
    wrapper.style.flexDirection = "column";
    wrapper.style.alignItems = isMe ? "flex-end" : "flex-start";
    wrapper.style.marginBottom = "4px";

    // Infos pour read receipts
    wrapper.dataset.senderId = senderId.toString();
    wrapper.dataset.msgStatus = isMe
        ? (isReadByOther ? "read" : "sent")
        : "none";

    const bubble = document.createElement("div");
    bubble.style.maxWidth = "70%";
    bubble.style.padding = "8px 10px";
    bubble.style.borderRadius = "14px";
    bubble.style.fontSize = "0.85rem";

    if (isMe) {
        bubble.style.background = "linear-gradient(135deg, #4f46e5, #6366f1)";
        bubble.style.borderBottomRightRadius = "4px";
    } else {
        bubble.style.background = "rgba(22,30,70,0.95)";
        bubble.style.borderBottomLeftRadius = "4px";
    }

    if (messageType === "audio" && attachmentUrl) {
        const audio = document.createElement("audio");
        audio.controls = true;
        audio.style.width = "200px";
        audio.src = attachmentUrl;
        bubble.appendChild(audio);
    } else if (messageType === "image" && attachmentUrl) {
        const img = document.createElement("img");
        img.src = attachmentUrl;
        img.alt = content && content !== "[image]" ? content : "Image";
        img.style.maxWidth = "220px";
        img.style.borderRadius = "10px";
        img.style.display = "block";
        img.style.cursor = "zoom-in";

        img.addEventListener("click", function (ev) {
            ev.stopPropagation();
            openImageOverlay(attachmentUrl);
        });

        bubble.appendChild(img);
    } else if (messageType === "file" && attachmentUrl) {
        const link = document.createElement("a");
        link.href = attachmentUrl;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.style.color = "#bfdbfe";
        link.style.textDecoration = "none";
        link.style.display = "inline-flex";
        link.style.alignItems = "center";
        link.style.gap = "6px";

        const icon = document.createElement("span");
        icon.textContent = "📎";
        link.appendChild(icon);

        const nameSpan = document.createElement("span");
        nameSpan.textContent = content || "Fichier joint";
        link.appendChild(nameSpan);

        bubble.appendChild(link);
    } else {
        bubble.textContent = content;
    }

    wrapper.appendChild(bubble);

    if (timestampText) {
        const dateLine = document.createElement("div");
        dateLine.style.fontSize = "0.7rem";
        dateLine.style.color = "rgba(255,255,255,0.6)";
        dateLine.style.marginTop = "2px";
        dateLine.style.padding = "0 4px";

        const dateSpan = document.createElement("span");
        dateSpan.textContent = timestampText;
        dateLine.appendChild(dateSpan);

        if (isMe) {
            const sepSpan = document.createElement("span");
            sepSpan.textContent = " · ";
            dateLine.appendChild(sepSpan);

            const statusSpan = document.createElement("span");
            const statusSpanId = "msg-status-" + (message.id || message.Id || (Date.now() + Math.random()));
            statusSpan.id = statusSpanId;
            wrapper.dataset.statusSpanId = statusSpanId;

            statusSpan.textContent = isReadByOther ? "Vu" : "Envoyé";
            dateLine.appendChild(statusSpan);
        }

        wrapper.appendChild(dateLine);
    }

    container.appendChild(wrapper);
    container.scrollTop = container.scrollHeight;
}
