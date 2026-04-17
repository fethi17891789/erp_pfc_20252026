// Fichier : Donnees/BlockchainAncrage.cs
namespace Donnees
{
    /// <summary>
    /// Représente l'ancrage cryptographique d'un document sur la blockchain Sepolia.
    /// Chaque ancrage contient le hash SHA-256 du document et la preuve on-chain (TxHash).
    /// </summary>
    public class BlockchainAncrage
    {
        public int Id { get; set; }

        /// <summary>Type du document : "OF", "OA" ou "TRAJET"</summary>
        public string TypeDocument { get; set; } = string.Empty;

        /// <summary>Référence unique du document (ex: OF-0001-20260417143022)</summary>
        public string RefDocument { get; set; } = string.Empty;

        /// <summary>Empreinte SHA-256 du contenu original (hex 64 caractères)</summary>
        public string HashContenu { get; set; } = string.Empty;

        /// <summary>Hash de transaction Ethereum retourné par Sepolia</summary>
        public string? TxHash { get; set; }

        /// <summary>Lien direct Etherscan vers la transaction</summary>
        public string? LienEtherscan { get; set; }

        /// <summary>Statut : "Ancre" (sur Sepolia), "Local" (hash seul), "ErreurSepolia"</summary>
        public string Statut { get; set; } = "Local";

        public DateTime DateAncrage { get; set; } = DateTime.UtcNow;

        public int? CreeParUserId { get; set; }
    }
}
