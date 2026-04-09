/**
 * Presence.js - Gestion globale de la présence via SignalR
 * Ce script est chargé sur toutes les pages de l'ERP pour assurer que l'utilisateur
 * est marqué comme "En ligne" tant qu'il navigue sur le site.
 */

(function () {
    const userId = window.currentUserIdFromServer;

    if (!userId || userId <= 0) {
        console.log("[Presence] Aucun utilisateur authentifié, connexion SignalR ignorée.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub?userId=" + encodeURIComponent(userId))
        .withAutomaticReconnect()
        .build();

    // Export global pour que les modules (ex: Messagerie.js) puissent le réutiliser
    window.presenceConnection = connection;

    // Gestion de la reconnexion
    connection.onreconnecting(error => {
        console.warn("[Presence] Reconnexion en cours...", error);
    });

    connection.onreconnected(connectionId => {
        console.log("[Presence] Reconnecté. ConnectionID:", connectionId);
    });

    // Écoute globale des notifications de messages
    connection.on("ReceiveNotification", function (data) {
        if (!data) return;

        // Si on est déjà sur la messagerie avec cet utilisateur, on ignore le toast
        var senderId = data.SenderId || data.senderId;
        if (window.location.pathname.indexOf("/Messagerie") !== -1) {
            var params = new URLSearchParams(window.location.search);
            var chatUserId = params.get("userId");
            if (chatUserId && parseInt(chatUserId) === senderId) return;
        }

        // Afficher le toast iPhone (défini dans Notifications.js)
        if (typeof showNotificationToast === "function") {
            showNotificationToast(data);
        }
    });

    // Démarrage de la connexion
    connection.start()
        .then(() => {
            console.log("[Presence] SignalR connecté.");
            
            // Si on est sur la page de messagerie, on peut notifier que la connexion est prête
            if (typeof onPresenceConnected === "function") {
                onPresenceConnected();
            }
        })
        .catch(err => {
            console.error("[Presence] Erreur lors du démarrage SignalR:", err.toString());
        });

})();
