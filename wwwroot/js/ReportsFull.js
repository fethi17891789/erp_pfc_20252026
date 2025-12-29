document.addEventListener('DOMContentLoaded', function () {
    // Gestion du bouton retour
    const backBtn = document.querySelector('.reports-full-back-btn');
    if (backBtn) {
        backBtn.addEventListener('click', function () {
            // Redirection vers la page d'accueil (à adapter selon ta navigation)
            window.history.back();
        });
    }

    // Animation d'entrée des cartes d'action
    const actionCards = document.querySelectorAll('.reports-full-action-card');
    actionCards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        setTimeout(() => {
            card.style.transition = 'all 0.6s cubic-bezier(0.16, 1, 0.3, 1)';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, index * 200);
    });

    // Effet de survol sur les cartes fonctionnalités
    const featureCards = document.querySelectorAll('.feature-card');
    featureCards.forEach(card => {
        card.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-4px) scale(1.02)';
        });

        card.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0) scale(1)';
        });
    });

    // Placeholder pour "Mes formulaires" - à implémenter plus tard
    const ctaBtn = document.querySelector('.reports-full-cta-btn');
    if (ctaBtn) {
        ctaBtn.addEventListener('click', function (e) {
            e.preventDefault();
            // TODO: Navigation vers la liste des formulaires
            alert('Fonctionnalité "Mes formulaires" en développement 🚀');
        });
    }
});
