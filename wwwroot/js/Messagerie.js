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
let typingTimeout = null;

// Connexion SignalR (on passe le userId dans l'URL)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub?userId=" + encodeURIComponent(currentUserId))
    .withAutomaticReconnect()
    .build();

// ====================== GESTION STATUT EN LIGNE / HORS LIGNE ======================

function updateUserItemOnlineStatus(userId, isOnline) {
    const container = document.getElementById("usersContainer");
    if (!container) return;

    const items = container.querySelectorAll(".user-item");
    items.forEach(item => {
        const idStr = item.getAttribute("data-user-id");
        if (!idStr) return;

        const id = parseInt(idStr, 10);
        if (id !== userId) return;

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

connection.on("ReceiveMessage", function (message) {
    if (!message || (!message.conversationId && !message.ConversationId)) return;

    const convId = message.conversationId || message.ConversationId;
    if (convId !== currentConversationId) return;

    if (typeof message.isReadByOther === "undefined" && typeof message.IsReadByOther === "undefined") {
        message.isReadByOther = false;
    }

    appendMessageToUi(message);

    const senderId = message.senderId || message.SenderId;
    if (senderId && senderId !== currentUserId) {
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("MarkConversationAsRead", currentConversationId, currentUserId)
                .catch(err => console.warn("Erreur MarkConversationAsRead:", err.toString()));
        }
    }
});

connection.on("MessageUpdated", function (message) {
    if (currentTargetUserId) {
        const activeUserItem = document.querySelector(`.user-item[data-user-id='${currentTargetUserId}']`);
        if (activeUserItem) activeUserItem.click();
    }
});

connection.on("ConversationRead", function (info) {
    if (!info) return;

    const convId = info.conversationId || info.ConversationId;
    const readerUserId = info.readerUserId || info.ReaderUserId;

    if (!currentConversationId || convId !== currentConversationId) return;
    if (readerUserId === currentUserId) return;

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

connection.on("UserOnline", function (info) {
    if (!info) return;
    const userId = info.userId || info.UserId;
    if (!userId) return;

    updateUserItemOnlineStatus(userId, true);

    if (currentTargetUserId && userId === currentTargetUserId) {
        updateHeaderOnlineStatus(true);
    }
});

connection.on("UserOffline", function (info) {
    if (!info) return;
    const userId = info.userId || info.UserId;
    if (!userId) return;

    updateUserItemOnlineStatus(userId, false);

    if (currentTargetUserId && userId === currentTargetUserId) {
        updateHeaderOnlineStatus(false);
    }
});

connection.on("UserTypingStatus", function (info) {
    if (!info) return;
    const convId = info.conversationId || info.ConversationId;
    const userId = info.userId || info.UserId;
    const isTyping = typeof info.isTyping !== "undefined" ? info.isTyping : info.IsTyping;
    const isAiBackend = typeof info.isAi !== "undefined" ? info.isAi : (info.IsAi || false);

    if (convId !== currentConversationId || userId === currentUserId) return;

    showTypingIndicator(isTyping, isAiBackend);
});

window.aiThinkingInterval = null;

function showTypingIndicator(isTyping, isAiBackend = false) {
    const container = document.getElementById("messagesContainer");
    if (!container) return;

    let indicator = document.getElementById("typingIndicator");

    if (isTyping) {
        if (!indicator) {
            indicator = document.createElement("div");
            indicator.id = "typingIndicator";
            indicator.className = "typing-indicator";
            
            let extraHtml = "";
            let isAi = isAiBackend || (currentTargetUserName && currentTargetUserName.toUpperCase() === "GEMINI");
            
            if (isAi) {
                extraHtml = `<div id="aiThinkingStatus" style="font-size:0.7rem; color:var(--text-muted-2); margin-left:8px; font-style:italic; font-weight:500; transition:opacity 0.3s ease; opacity:0.8;">Analyse de la demande...</div>`;
            }
            
            indicator.innerHTML = '<div style="display:flex; align-items:center; gap:4px; margin-right:4px;"><div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div></div>' + extraHtml;
            container.appendChild(indicator);
            container.scrollTop = container.scrollHeight;

            if (isAi) {
                const phrases = ["Génération de la réponse...", "Consultation des bases...", "Réflexion en cours...", "Optimisation du code...", "Presque prêt..."];
                let pIndex = 0;
                if (window.aiThinkingInterval) clearInterval(window.aiThinkingInterval);
                window.aiThinkingInterval = setInterval(() => {
                    const el = document.getElementById("aiThinkingStatus");
                    if (el) {
                        el.style.opacity = "0";
                        setTimeout(() => {
                            pIndex = (pIndex + 1) % phrases.length;
                            if (el) { el.textContent = phrases[pIndex]; el.style.opacity = "0.8"; }
                        }, 300);
                    } else {
                        clearInterval(window.aiThinkingInterval);
                    }
                }, 2500);
            }
        }
    } else {
        if (indicator) indicator.remove();
        if (window.aiThinkingInterval) clearInterval(window.aiThinkingInterval);
    }
}

function sendTypingStatus(isTyping) {
    if (connection.state === signalR.HubConnectionState.Connected && currentConversationId) {
        connection.invoke("SendTypingStatus", currentConversationId, currentUserId, isTyping)
            .catch(err => console.warn("Erreur SendTypingStatus:", err));
    }
}

connection.start().then(function () {
    console.log("SignalR connecté");
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
        return;
    }

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
            if (!response.ok) return;

            const conv = await response.json();

            if (currentConversationId && connection.state === signalR.HubConnectionState.Connected) {
                try {
                    if (typingTimeout) {
                        clearTimeout(typingTimeout);
                        typingTimeout = null;
                        sendTypingStatus(false);
                    }
                    await connection.invoke("LeaveConversation", currentConversationId);
                } catch (err) { }
            }

            currentConversationId = conv.conversationId;

            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke("JoinConversation", currentConversationId);
                } catch (err) { }
            }

            const otherName = conv.otherUserName || displayName;
            if (headerName) headerName.textContent = otherName;
            if (headerInitial) {
                if ((otherName || "").toUpperCase() === "GEMINI") {
                    headerInitial.innerHTML = '<img src="/images/gemini-logo.svg" style="width:20px; height:20px; object-fit:contain;" />';
                    headerInitial.style.background = "#ffffff";
                    headerInitial.style.boxShadow = "0 0 10px rgba(255,255,255,0.2)";
                } else {
                    const initial = (otherName || "?").charAt(0).toUpperCase();
                    headerInitial.textContent = initial;
                    headerInitial.style.background = "linear-gradient(135deg, var(--accent), var(--accent-hover))";
                    headerInitial.style.boxShadow = "none";
                }
            }
            if (headerStatusDot && headerStatusText) {
                updateHeaderOnlineStatus(isOnline);
            }

            messagesContainer.innerHTML = "";
            if (Array.isArray(conv.messages)) {
                conv.messages.forEach(m => appendMessageToUi(m));
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }

            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    connection.invoke("MarkConversationAsRead", currentConversationId, currentUserId)
                        .catch(err => { });
                } catch (err) { }
            }
        } catch (err) {
            console.error("Erreur chargement conversation :", err);
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
                // Arrêter l'indicateur de saisie après l'envoi
                if (typingTimeout) {
                    clearTimeout(typingTimeout);
                    typingTimeout = null;
                    sendTypingStatus(false);
                }
            })
            .catch(err => console.error(err.toString()));
    }

    btnSend.addEventListener("click", send);
    input.addEventListener("keyup", function (e) {
        if (e.key === "Enter") send();
    });

    input.addEventListener("input", function () {
        if (!currentConversationId) return;

        if (!typingTimeout) {
            sendTypingStatus(true);
        }

        clearTimeout(typingTimeout);
        typingTimeout = setTimeout(() => {
            sendTypingStatus(false);
            typingTimeout = null;
        }, 3000);
    });

    if (btnRecordAudio) {
        btnRecordAudio.addEventListener("click", toggleRecording);
    }

    if (btnAttachFile && fileInput) {
        btnAttachFile.addEventListener("click", function () {
            if (!currentConversationId || !currentTargetUserId) {
                alert("Sélectionne d'abord un destinataire.");
                return;
            }
            fileInput.click();
        });
        fileInput.addEventListener("change", function (e) {
            handleFileUploads(e.target.files);
            e.target.value = "";
        });
    }

    // --- Drag & Drop Support ---
    const chatMain = document.querySelector(".chat-main");
    const dropOverlay = document.getElementById("chatDropZoneOverlay");

    if (chatMain && dropOverlay) {
        chatMain.addEventListener("dragenter", function (e) {
            if (!currentConversationId || !currentTargetUserId) return;
            e.preventDefault();
            e.stopPropagation();
            dropOverlay.classList.add("drop-zone-active");
        });

        chatMain.addEventListener("dragover", function (e) {
            if (!currentConversationId || !currentTargetUserId) return;
            e.preventDefault();
            e.stopPropagation();
            dropOverlay.classList.add("drop-zone-active");
        });

        chatMain.addEventListener("dragleave", function (e) {
            e.preventDefault();
            e.stopPropagation();
            // On ne cache l'overlay que si on sort vraiment du conteneur parent
            if (!chatMain.contains(e.relatedTarget)) {
                dropOverlay.classList.remove("drop-zone-active");
            }
        });

        chatMain.addEventListener("drop", function (e) {
            e.preventDefault();
            e.stopPropagation();
            dropOverlay.classList.remove("drop-zone-active");

            if (!currentConversationId || !currentTargetUserId) return;
            if (e.dataTransfer && e.dataTransfer.files.length > 0) {
                handleFileUploads(e.dataTransfer.files);
            }
        });
    }
});

// ====================== GESTION AUDIO ======================

async function toggleRecording() {
    if (!currentConversationId || !currentTargetUserId) return;
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
                if (e.data && e.data.size > 0) recordedChunks.push(e.data);
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
        btnRecordAudio.classList.add("is-recording");
    } else {
        btnRecordAudio.classList.remove("is-recording");
    }
}

async function uploadAudioBlob(blob) {
    if (!blob || blob.size === 0 || !currentConversationId) return;

    const formData = new FormData();
    formData.append("conversationId", currentConversationId);
    formData.append("audioFile", blob, "audio.webm");

    const tokenInput = document.getElementById("__RequestVerificationToken");
    if (tokenInput) formData.append("__RequestVerificationToken", tokenInput.value);

    try {
        const response = await fetch("/Messagerie?handler=UploadAudio", { method: "POST", body: formData });
        if (!response.ok) return;

        const msg = await response.json();
        await connection.invoke("SendAudioMessage", msg);
    } catch (err) {
        console.error("Erreur upload audio:", err);
    }
}

// ====================== GESTION FICHIERS ======================

function handleFileUploads(files) {
    if (!files || files.length === 0 || !currentConversationId || !currentTargetUserId) return;

    const tokenInput = document.getElementById("__RequestVerificationToken");
    const token = tokenInput ? tokenInput.value : "";

    Array.from(files).forEach(file => {
        const formData = new FormData();
        formData.append("conversationId", currentConversationId);
        formData.append("chatFile", file);
        formData.append("__RequestVerificationToken", token);

        fetch("/Messagerie?handler=UploadFile", { method: "POST", body: formData })
            .then(r => r.json())
            .then(messageDto => {
                connection.invoke("SendMessage", messageDto);
            })
            .catch(err => console.error("Erreur upload fichier:", err));
    });
}

// ====================== UTILITAIRES MESSAGES ======================

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

function openImageOverlay(imageUrl) {
    const overlay = document.getElementById("chatImageOverlay");
    if (!overlay) return;
    const imgEl = document.getElementById("chatImageOverlayImg");
    if (imgEl) imgEl.src = imageUrl;
    overlay.style.display = "flex";
}

function closeImageOverlay() {
    const overlay = document.getElementById("chatImageOverlay");
    if (overlay) overlay.style.display = "none";
}

document.addEventListener("click", function (e) {
    const overlay = document.getElementById("chatImageOverlay");
    if (overlay && e.target === overlay) {
        closeImageOverlay();
    }
});

// ====================== CREATION DE LA BULLE ======================
function appendMessageToUi(message) {
    const container = document.getElementById("messagesContainer");
    if (!container) return;

    const senderId = message.senderId || message.SenderId;
    const content = message.content || message.Content || "";
    const messageType = message.messageType || message.MessageType || "text";
    const attachmentUrl = message.attachmentUrl || message.AttachmentUrl || null;
    const timestampRaw = message.timestamp || message.Timestamp || null;
    const timestampText = formatTimestampForDisplay(timestampRaw);

    const isReadByOther = (typeof message.isReadByOther !== "undefined" ? message.isReadByOther : message.IsReadByOther) || false;
    const isMe = senderId === currentUserId;

    // Wrapper pour aligner l'étiquette au bon endroit
    const wrapper = document.createElement("div");
    wrapper.style.display = "flex";
    wrapper.style.flexDirection = "column";
    wrapper.style.alignItems = isMe ? "flex-end" : "flex-start";
    wrapper.style.marginBottom = "4px";
    
    // Animation d'apparition
    wrapper.style.animation = "msg-bubble-appear 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275) forwards";
    wrapper.style.transformOrigin = isMe ? "bottom right" : "bottom left";

    wrapper.dataset.senderId = senderId.toString();
    wrapper.dataset.msgStatus = isMe ? (isReadByOther ? "read" : "sent") : "none";

    // ==========================================================
    // CAS SPÉCIAL OA 
    // ==========================================================
    if (messageType === "oa-proposal" || messageType === "oa_request") {

        const parser = new DOMParser();
        const doc = parser.parseFromString(content, 'text/html');
        // La grosse carte preview
        const card = doc.querySelector('.oa-preview-card');

        let bulleToDisplay = null;

        if (card && card.children.length >= 2) {
            const vueExpediteur = card.children[0];
            const vueDestinataire = card.children[1];

            // On extrait JUSTE la ligne contenant la bulle pour garder ton design CSS d'origine !
            if (isMe) {
                bulleToDisplay = vueExpediteur.querySelector('.chat-message-row');
            } else {
                bulleToDisplay = vueDestinataire.querySelector('.chat-message-row');

                // On relie tes veritables boutons Accepter/Refuser
                if (bulleToDisplay) {
                    const btnAccept = bulleToDisplay.querySelector('.oa-btn-accept');
                    const btnReject = bulleToDisplay.querySelector('.oa-btn-reject');
                    const currentMsgId = message.id || message.Id;

                    if (btnAccept && !btnAccept.dataset.bound) {
                        btnAccept.dataset.bound = "true";
                        btnAccept.addEventListener('click', () => handleOaAction(content, 'acceptee', currentMsgId));
                    }
                    if (btnReject && !btnReject.dataset.bound) {
                        btnReject.dataset.bound = "true";
                        btnReject.addEventListener('click', () => handleOaAction(content, 'refusee', currentMsgId));
                    }
                }
            }
        }

        if (bulleToDisplay) {
            // Pas de couleurs forcées, pas de padding forcé : On utilise ton CSS d'origine !
            bulleToDisplay.style.margin = "0";
            bulleToDisplay.style.maxWidth = "85%";
            wrapper.appendChild(bulleToDisplay);
        } else {
            const tempDiv = document.createElement("div");
            tempDiv.innerHTML = content;
            wrapper.appendChild(tempDiv);
        }

        if (timestampText) {
            const dateLine = document.createElement("div");
            dateLine.style.fontSize = "0.7rem";
            dateLine.style.color = "rgba(255,255,255,0.6)";
            dateLine.style.marginTop = "2px";
            dateLine.style.padding = "0 4px";
            const dateSpan = document.createElement("span");
            dateSpan.textContent = timestampText;
            dateLine.appendChild(dateSpan);
            wrapper.appendChild(dateLine);
        }

        container.appendChild(wrapper);
        container.scrollTop = container.scrollHeight;
        return;
    }

    // =========================================================
    // Bulle standard pour les Mots Normaux / Fichiers / etc.
    // =========================================================
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
        const player = createCustomAudioPlayer(attachmentUrl);
        bubble.appendChild(player);
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

// ====================== GESTION DES ACTIONS OA ======================
window.handleOaAction = function (originalContentHtml, action, messageId) {
    if (!messageId) return;

    const parser = new DOMParser();
    const doc = parser.parseFromString(originalContentHtml, 'text/html');
    const card = doc.querySelector('.oa-preview-card');

    if (!card || card.children.length < 2) return;

    const vueExpediteur = card.children[0];
    const vueDestinataire = card.children[1];

    const isAccepte = (action === 'acceptee');
    const color = isAccepte ? "#10b981" : "#ef4444"; // Vert ou Rouge
    const texte = isAccepte ? "Acceptée ✅" : "Refusée ❌";

    // Ajout d'une classe pour détection backend
    if (isAccepte) card.classList.add('oa-status-acceptee');
    else card.classList.remove('oa-status-acceptee');

    // MAJ de la vue de l'expéditeur
    const statusExpediteur = vueExpediteur.querySelector('.oa-status-pill');
    if (statusExpediteur) {
        statusExpediteur.className = "oa-status-pill";
        statusExpediteur.style.backgroundColor = color;
        statusExpediteur.style.color = "white";
        statusExpediteur.innerHTML = `<span>${texte}</span>`;
    }

    // MAJ de la vue de l'acheteur
    const actionsBlock = vueDestinataire.querySelector('.oa-actions');
    if (actionsBlock) actionsBlock.remove();

    const footerAcheteur = vueDestinataire.querySelector('.oa-footer');
    if (footerAcheteur) {
        footerAcheteur.style.background = color;
        footerAcheteur.style.color = "white";
        footerAcheteur.innerHTML = `<span style="display:block; padding:4px; font-weight:bold; width:100%; text-align:center;">Décision : ${texte}</span>`;
    }

    const newHtmlContent = card.outerHTML;
    if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("UpdateOaHtml", parseInt(messageId), newHtmlContent)
            .catch(err => console.error("Erreur backend:", err));
    }
};

function createCustomAudioPlayer(url) {
    const container = document.createElement("div");
    container.className = "audio-player";

    const playBtn = document.createElement("button");
    playBtn.className = "audio-play-btn";
    playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';

    const rightCol = document.createElement("div");
    rightCol.className = "audio-content-right";

    const visualRow = document.createElement("div");
    visualRow.className = "audio-visual-row";

    const progressContainer = document.createElement("div");
    progressContainer.className = "audio-progress-container";
    const progressBar = document.createElement("div");
    progressBar.className = "audio-progress-bar";
    progressContainer.appendChild(progressBar);

    const waves = document.createElement("div");
    waves.className = "audio-waves";
    for (let i = 0; i < 12; i++) {
        const bar = document.createElement("div");
        bar.className = "audio-wave-bar";
        bar.style.height = (4 + Math.random() * 10) + "px";
        waves.appendChild(bar);
    }

    visualRow.appendChild(progressContainer);
    visualRow.appendChild(waves);

    const infoRow = document.createElement("div");
    infoRow.className = "audio-info-row";
    const timeLabel = document.createElement("span");
    timeLabel.textContent = "00:00";
    const titleLabel = document.createElement("span");
    titleLabel.textContent = "Vocal";
    infoRow.appendChild(titleLabel);
    infoRow.appendChild(timeLabel);

    rightCol.appendChild(visualRow);
    rightCol.appendChild(infoRow);

    container.appendChild(playBtn);
    container.appendChild(rightCol);

    const audio = new Audio(url);

    playBtn.onclick = (e) => {
        e.stopPropagation();
        if (audio.paused) {
            audio.play();
            playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/></svg>';
        } else {
            audio.pause();
            playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
        }
    };

    audio.ontimeupdate = () => {
        const pct = (audio.currentTime / audio.duration) * 100;
        progressBar.style.width = pct + "%";

        const m = Math.floor(audio.currentTime / 60);
        const s = Math.floor(audio.currentTime % 60);
        timeLabel.textContent = `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    };

    audio.onended = () => {
        playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
        progressBar.style.width = "0%";
    };

    progressContainer.onclick = (e) => {
        e.stopPropagation();
        const rect = progressContainer.getBoundingClientRect();
        const pct = (e.clientX - rect.left) / rect.width;
        if (!isNaN(audio.duration)) {
            audio.currentTime = pct * audio.duration;
        }
    };

    return container;
}
