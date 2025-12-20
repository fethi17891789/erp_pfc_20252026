// BDDView.js - Toggle password visibility

document.addEventListener("DOMContentLoaded", function () {
    var toggles = document.querySelectorAll(".show-password");
    toggles.forEach(function (toggle) {
        toggle.addEventListener("click", function () {
            var targetId = this.getAttribute("data-target");
            var input = document.getElementById(targetId);
            if (input) {
                input.type = input.type === "password" ? "text" : "password";
            }
        });
    });
});
