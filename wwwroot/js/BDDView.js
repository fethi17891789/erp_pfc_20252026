// BDDView.js - Logic spécifique à la page de configuration BDD

function togglePasswordVisibility(targetId) {
    var input = document.getElementById(targetId);
    if (!input) return;

    input.type = input.type === "password" ? "text" : "password";
}

// Attache les événements après chargement du DOM
document.addEventListener("DOMContentLoaded", function () {
    var toggles = document.querySelectorAll(".show-password[data-target]");
    toggles.forEach(function (toggle) {
        toggle.addEventListener("click", function () {
            var targetId = this.getAttribute("data-target");
            togglePasswordVisibility(targetId);
        });
    });
});
