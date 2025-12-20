// Home.js - Modal de déconnexion

window.openLogoutModal = function () {
    const overlay = document.getElementById('logout-overlay');
    if (overlay) {
        overlay.style.display = 'flex';
        document.body.classList.add('logout-blur-active');
    }
};

window.closeLogoutModal = function () {
    const overlay = document.getElementById('logout-overlay');
    if (overlay) {
        overlay.style.display = 'none';
        document.body.classList.remove('logout-blur-active');
    }
};
