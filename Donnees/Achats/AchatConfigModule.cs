// Fichier : Donnees/Achats/AchatConfigModule.cs
using System;

namespace Donnees.Achats
{
    /// <summary>
    /// Politique de mise à jour du prix d'achat sur la fiche produit.
    /// Choisie une fois au premier lancement du module Achats.
    /// </summary>
    public enum PolitiquePrixAchat
    {
        /// <summary>Option A (recommandée) : le prix du produit est mis à jour avec le dernier prix d'achat réel.</summary>
        DernierPrix = 0,

        /// <summary>Option B : le prix du produit est recalculé comme une moyenne pondérée des derniers achats.</summary>
        MoyennePonderee = 1
    }

    /// <summary>
    /// Configuration unique du module Achats (une seule ligne en base).
    /// </summary>
    public class AchatConfigModule
    {
        public int Id { get; set; }

        /// <summary>Politique de mise à jour du prix d'achat sur les produits.</summary>
        public PolitiquePrixAchat PolitiquePrix { get; set; } = PolitiquePrixAchat.DernierPrix;

        /// <summary>Indique si le module a été configuré au moins une fois (affichage de l'écran de bienvenue).</summary>
        public bool EstConfigure { get; set; } = false;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public int? CreeParUserId { get; set; }
    }
}
