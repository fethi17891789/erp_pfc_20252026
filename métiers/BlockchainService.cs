// Fichier : métiers/BlockchainService.cs
using System.Security.Cryptography;
using System.Text;
using Donnees;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Metier
{
    // ── Mapping Nethereum → fonction Solidity enregistrerDocument(string, bytes32, string) ──
    [Function("enregistrerDocument")]
    public class EnregistrerDocumentFunction : FunctionMessage
    {
        [Parameter("string", "reference", 1)]
        public string Reference { get; set; } = string.Empty;

        [Parameter("bytes32", "hashContenu", 2)]
        public byte[] HashContenu { get; set; } = Array.Empty<byte>();

        [Parameter("string", "typeDocument", 3)]
        public string TypeDocument { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service de traçabilité blockchain pour SKYRA ERP.
    /// Calcule le hash SHA-256 de chaque document sensible (OF, OA, TRAJET) et l'ancre
    /// sur la blockchain publique Ethereum Sepolia Testnet via un smart contract dédié.
    /// </summary>
    public class BlockchainService
    {
        private readonly ErpDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<Metier.Messagerie.ChatHub> _hubContext;
        private readonly bool _estActive;
        private readonly string _rpcUrl;
        private readonly string _privateKey;
        private readonly string _contratAdresse;

        public BlockchainService(ErpDbContext db, IConfiguration config, IServiceScopeFactory scopeFactory, IHubContext<Metier.Messagerie.ChatHub> hubContext)
        {
            _db = db;
            _scopeFactory      = scopeFactory;
            _hubContext        = hubContext;
            _estActive         = config.GetValue<bool>("Blockchain:EstActive");
            _rpcUrl            = config["Blockchain:SepoliaRpcUrl"] ?? "https://rpc.sepolia.org";
            _privateKey        = config["Blockchain:WalletPrivateKey"] ?? "";
            _contratAdresse    = config["Blockchain:ContratAdresse"] ?? "";
        }

        // ─────────────────────────────────────────────────────────────
        // HACHAGE
        // ─────────────────────────────────────────────────────────────

        /// <summary>Retourne le hash SHA-256 hexadécimal d'un tableau d'octets (PDF, JSON…).</summary>
        public static string CalculerHash(byte[] contenu)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(contenu)).ToLower();
        }

        /// <summary>Retourne le hash SHA-256 hexadécimal d'une chaîne UTF-8.</summary>
        public static string CalculerHash(string contenu)
            => CalculerHash(Encoding.UTF8.GetBytes(contenu));

        // ─────────────────────────────────────────────────────────────
        // ANCRAGE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ancre un document sur la blockchain Sepolia et persiste l'ancrage en base.
        /// Si Sepolia n'est pas configuré (EstActive=false), l'ancrage reste "Local"
        /// avec le hash SHA-256 — le document est tout de même protégé en intégrité.
        /// Cette méthode ne bloque JAMAIS la génération du document.
        /// </summary>
        public async Task<BlockchainAncrage> AncrerDocumentAsync(
            string typeDocument,
            string refDocument,
            byte[] contenuDocument,
            int? creeParUserId = null)
        {
            var hash = CalculerHash(contenuDocument);

            // 1. Sauvegarder immédiatement en local — ne bloque PAS l'appel AJAX
            var ancrage = new BlockchainAncrage
            {
                TypeDocument  = typeDocument,
                RefDocument   = refDocument,
                HashContenu   = hash,
                DateAncrage   = DateTime.UtcNow,
                CreeParUserId = creeParUserId,
                Statut        = "Local"
            };

            _db.BlockchainAncrages.Add(ancrage);
            await _db.SaveChangesAsync();

            // 2. Tenter l'ancrage Sepolia EN ARRIÈRE-PLAN — sans bloquer le retour
            if (_estActive
                && !string.IsNullOrWhiteSpace(_privateKey)
                && !string.IsNullOrWhiteSpace(_contratAdresse))
            {
                var ancrageId = ancrage.Id;
                var refCopy   = refDocument;
                var hashCopy  = hash;
                var typeCopy  = typeDocument;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var txHash = await EnvoyerVersSepolia(refCopy, hashCopy, typeCopy);
                        using var scope = _scopeFactory.CreateScope();
                        var db2 = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                        var a = await db2.BlockchainAncrages.FindAsync(ancrageId);
                        if (a != null)
                        {
                            a.TxHash        = txHash;
                            a.LienEtherscan = $"https://sepolia.etherscan.io/tx/{txHash}";
                            a.Statut        = "Ancre";
                            await db2.SaveChangesAsync();
                        }
                        // Notifier tous les clients connectés que l'ancrage est confirmé
                        await _hubContext.Clients.All.SendAsync("AncrageBlockchainConfirme", new
                        {
                            refDocument  = refCopy,
                            txHash,
                            lienEtherscan = $"https://sepolia.etherscan.io/tx/{txHash}",
                            typeDocument = typeCopy
                        });
                        Console.WriteLine($"[BLOCKCHAIN] Ancrage Sepolia réussi : {refCopy} → {txHash}");
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db2 = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                            var a = await db2.BlockchainAncrages.FindAsync(ancrageId);
                            if (a != null)
                            {
                                a.Statut = "ErreurSepolia";
                                await db2.SaveChangesAsync();
                            }
                        }
                        catch { /* ignore les erreurs de mise à jour du statut */ }
                        Console.WriteLine($"[BLOCKCHAIN] Erreur Sepolia pour {refCopy} : {ex.GetType().Name} — {ex.Message}");
                    }
                });
            }

            return ancrage;
        }

        // ─────────────────────────────────────────────────────────────
        // VÉRIFICATION D'INTÉGRITÉ
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Vérifie qu'un document n'a pas été altéré depuis son ancrage initial.
        /// Retourne (true, ancrage) si intègre, (false, ancrage) si modifié.
        /// </summary>
        public async Task<(bool estIntegre, BlockchainAncrage? ancrage)> VerifierIntegriteAsync(
            string refDocument,
            byte[] contenuActuel)
        {
            var ancrage = await _db.BlockchainAncrages
                .FirstOrDefaultAsync(a => a.RefDocument == refDocument);

            if (ancrage == null) return (false, null);

            var hashActuel = CalculerHash(contenuActuel);
            return (hashActuel == ancrage.HashContenu, ancrage);
        }

        /// <summary>Récupère les N derniers ancrages (pour le dashboard blockchain).</summary>
        public async Task<List<BlockchainAncrage>> GetAncragesRecentAsync(int limit = 50)
        {
            return await _db.BlockchainAncrages
                .OrderByDescending(a => a.DateAncrage)
                .Take(limit)
                .ToListAsync();
        }

        /// <summary>Récupère l'ancrage d'un document précis par sa référence.</summary>
        public async Task<BlockchainAncrage?> GetAncrageParRefAsync(string refDocument)
        {
            return await _db.BlockchainAncrages
                .FirstOrDefaultAsync(a => a.RefDocument == refDocument);
        }

        // ─────────────────────────────────────────────────────────────
        // INTERACTION SMART CONTRACT SEPOLIA
        // ─────────────────────────────────────────────────────────────

        private async Task<string> EnvoyerVersSepolia(string reference, string hashHex, string typeDoc)
        {
            // Plusieurs RPC Sepolia publics en cas de défaillance de l'un d'eux
            var rpcUrls = new[]
            {
                _rpcUrl,
                "https://ethereum-sepolia-rpc.publicnode.com",
                "https://rpc2.sepolia.org",
                "https://sepolia.drpc.org"
            };

            // Clé privée : s'assurer qu'elle commence par 0x
            var privateKey = _privateKey.StartsWith("0x") ? _privateKey : "0x" + _privateKey;

            // Sepolia chain ID = 11155111
            var account = new Account(privateKey, 11155111L);

            // Convertir le hash hex (64 chars) en bytes32 pour Solidity
            var hashBytes  = Convert.FromHexString(hashHex);
            var hashPadded = new byte[32];
            var copyOffset = 32 - hashBytes.Length;
            Array.Copy(hashBytes, 0, hashPadded, copyOffset, hashBytes.Length);

            var fonction = new EnregistrerDocumentFunction
            {
                Reference    = reference,
                HashContenu  = hashPadded,
                TypeDocument = typeDoc
            };

            Exception? derniereErreur = null;

            foreach (var rpc in rpcUrls)
            {
                try
                {
                    Console.WriteLine($"[BLOCKCHAIN] Tentative RPC : {rpc}");
                    var web3    = new Web3(account, rpc);
                    var handler = web3.Eth.GetContractTransactionHandler<EnregistrerDocumentFunction>();
                    var receipt = await handler.SendRequestAndWaitForReceiptAsync(_contratAdresse, fonction);
                    Console.WriteLine($"[BLOCKCHAIN] Succès via {rpc}");
                    return receipt.TransactionHash;
                }
                catch (Exception ex)
                {
                    derniereErreur = ex;
                    Console.WriteLine($"[BLOCKCHAIN] RPC {rpc} échoué : {ex.Message}");
                }
            }

            throw derniereErreur ?? new Exception("Tous les RPC Sepolia ont échoué.");
        }
    }
}
