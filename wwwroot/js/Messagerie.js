// Fichier : wwwroot/js/messagerie.js

// Connexion SignalR
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .withAutomaticReconnect()
    .build();

// ID utilisateur courant récupéré depuis la page Razor
// (window.currentUserIdFromServer est défini dans Messagerie.cshtml)
let currentUserId = window.currentUserIdFromServer || 0;
let currentUserName = "Vous";

let currentTargetUserId = null;
let currentTargetUserName = null;
let currentConversationId = null;

// Variables pour l'audio
let mediaRecorder = null;
let recordedChunks = [];
let isRecording = false;

// Réception d'un message temps réel
connection.on("ReceiveMessage", function (message) {
    if (!message || !message.conversationId) return;
    if (message.conversationId !== currentConversationId) return;

    appendMessageToUi(message);
});

// Démarrer la connexion
connection.start().then(function () {
    console.log("SignalR connecté");
}).catch(function (err) {
    console.error(err.toString());
});

// Logique UI
document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("chatInput");
    const btnSend = document.getElementById("btnSend");
    const usersContainer = document.getElementById("usersContainer");
    const headerName = document.getElementById("chatTargetName");
    const headerInitial = document.getElementById("chatTargetInitial");
    const headerSubtitle = document.getElementById("chatHeaderSubtitle");
    const messagesContainer = document.getElementById("messagesContainer");
    const btnRecordAudio = document.getElementById("btnRecordAudio");

    if (!input || !btnSend || !usersContainer || !messagesContainer) return;

    // Clic sur un utilisateur dans la liste de gauche
    usersContainer.addEventListener("click", async function (e) {
        const btn = e.target.closest(".user-item");
        if (!btn) return;

        const userIdStr = btn.getAttribute("data-user-id");
        const login = btn.getAttribute("data-login") || "";
        const email = btn.getAttribute("data-email") || "";
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
            // Appel au handler Razor Pages pour récupérer/créer la conversation et charger l'historique
            const url = `/Messagerie?handler=Conversation&otherUserId=${encodeURIComponent(userId)}`;
            const response = await fetch(url, { method: "GET" });
            if (!response.ok) {
                console.error("Erreur HTTP Conversation", response.status);
                return;
            }

            const conv = await response.json();
            // conv : { conversationId, otherUserId, otherUserName, messages: [...] }

            // Quitter l'ancienne conversation SignalR
            if (currentConversationId && connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke("LeaveConversation", currentConversationId);
                } catch (err) {
                    console.warn("Erreur LeaveConversation:", err.toString());
                }
            }

            currentConversationId = conv.conversationId;

            // Rejoindre la nouvelle conversation
            if (connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke("JoinConversation", currentConversationId);
                } catch (err) {
                    console.error("Erreur JoinConversation:", err.toString());
                }
            }

            // Mettre à jour le header
            if (headerName) headerName.textContent = conv.otherUserName || displayName;
            if (headerSubtitle) headerSubtitle.textContent = "Conversation directe";
            if (headerInitial) {
                const initial = (conv.otherUserName || displayName || "?").charAt(0).toUpperCase();
                headerInitial.textContent = initial;
            }

            // Afficher l'historique
            messagesContainer.innerHTML = "";
            if (Array.isArray(conv.messages)) {
                conv.messages.forEach(m => appendMessageToUi(m));
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
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
            content: content
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
});

// Démarrer/arrêter l'enregistrement
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
        btnRecordAudio.textContent = "⏹"; // bouton stop
        btnRecordAudio.style.color = "#f97373";
    } else {
        btnRecordAudio.textContent = "🎤";
        btnRecordAudio.style.color = "var(--text-muted-2)";
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
        // msg : ChatMessageDto (MessageType = "audio", AttachmentUrl = "...")

        // On ne l'affiche pas ici pour éviter le doublon,
        // SignalR le renvoie via ReceiveMessage pour tout le monde.

        try {
            await connection.invoke("SendAudioMessage", msg);
        } catch (err) {
            console.warn("Erreur SendAudioMessage:", err.toString());
        }
    } catch (err) {
        console.error("Erreur upload audio:", err);
    }
}

// Utilitaire format date -> "dd/MM HH:mm"
function formatTimestampForDisplay(timestampValue) {
    if (!timestampValue) return "";

    // message.Timestamp (C# DateTime) arrive généralement en ISO : "2026-01-24T18:59:00Z" ou similaire
    const d = new Date(timestampValue);
    if (isNaN(d.getTime())) return "";

    const day = d.getDate().toString().padStart(2, "0");
    const month = (d.getMonth() + 1).toString().padStart(2, "0");
    const hours = d.getHours().toString().padStart(2, "0");
    const minutes = d.getMinutes().toString().padStart(2, "0");

    return `${day}/${month} ${hours}:${minutes}`;
}

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

    const isMe = senderId === currentUserId;

    const wrapper = document.createElement("div");
    wrapper.style.display = "flex";
    wrapper.style.flexDirection = "column";
    wrapper.style.alignItems = isMe ? "flex-end" : "flex-start";
    wrapper.style.marginBottom = "4px";

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
    } else {
        bubble.textContent = content;
    }

    wrapper.appendChild(bubble);

    // Ligne date sous la bulle
    if (timestampText) {
        const dateLine = document.createElement("div");
        dateLine.style.fontSize = "0.7rem";
        dateLine.style.color = "rgba(255,255,255,0.6)";
        dateLine.style.marginTop = "2px";
        dateLine.style.padding = "0 4px";
        dateLine.textContent = timestampText;
        wrapper.appendChild(dateLine);
    }

    container.appendChild(wrapper);
    container.scrollTop = container.scrollHeight;
}
