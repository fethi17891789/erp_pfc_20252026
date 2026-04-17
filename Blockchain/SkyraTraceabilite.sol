// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * @title SKYRA ERP — Traçabilité de Documents
 * @notice Enregistre de manière immuable les empreintes SHA-256 des documents
 *         officiels générés par l'ERP SKYRA (Ordres de Fabrication, Ordres d'Achat,
 *         Trajets logistiques).
 *
 * Déploiement :
 *   1. Ouvrir https://remix.ethereum.org
 *   2. Créer ce fichier dans l'IDE Remix
 *   3. Compiler avec Solidity 0.8.20
 *   4. Deployer sur le réseau "Injected Provider - MetaMask" (choisir Sepolia)
 *   5. Copier l'adresse du contrat déployé dans appsettings.json → Blockchain:ContratAdresse
 */
contract SkyraTraceabilite {

    // ──────────────────────────────────────────────────────────────────
    // STRUCTURES
    // ──────────────────────────────────────────────────────────────────

    struct DocumentAncre {
        bytes32 hashContenu;    // Empreinte SHA-256 du contenu original
        string  typeDocument;   // "OF", "OA" ou "TRAJET"
        string  refDoc;         // Ex : "OF-0001-20260417143022"
        uint256 horodatage;     // Horodatage Unix au moment de l'ancrage
        address enregistrePar;  // Adresse Ethereum du wallet SKYRA
    }

    // ──────────────────────────────────────────────────────────────────
    // STOCKAGE
    // ──────────────────────────────────────────────────────────────────

    // refDoc → document ancré
    mapping(string => DocumentAncre) private _documents;

    // Liste ordonnée de toutes les références
    string[] private _listeRefs;

    // Adresse du propriétaire du contrat (wallet SKYRA)
    address public proprietaire;

    // ──────────────────────────────────────────────────────────────────
    // ÉVÉNEMENTS (visibles sur Etherscan)
    // ──────────────────────────────────────────────────────────────────

    event DocumentEnregistre(
        string  indexed refDoc,
        bytes32         hashContenu,
        string          typeDocument,
        uint256         horodatage,
        address         enregistrePar
    );

    // ──────────────────────────────────────────────────────────────────
    // CONSTRUCTEUR
    // ──────────────────────────────────────────────────────────────────

    constructor() {
        proprietaire = msg.sender;
    }

    // ──────────────────────────────────────────────────────────────────
    // FONCTIONS D'ÉCRITURE
    // ──────────────────────────────────────────────────────────────────

    /**
     * @notice Enregistre l'empreinte d'un document sur la blockchain.
     * @param refDoc       Référence unique du document (ex: "OF-0001-20260417143022")
     * @param hashContenu  Hash SHA-256 du contenu (bytes32)
     * @param typeDocument Type du document : "OF", "OA" ou "TRAJET"
     */
    function enregistrerDocument(
        string  memory refDoc,
        bytes32        hashContenu,
        string  memory typeDocument
    ) external {
        require(bytes(refDoc).length > 0,             "Ref vide");
        require(_documents[refDoc].horodatage == 0,   "Document deja enregistre");

        _documents[refDoc] = DocumentAncre({
            hashContenu   : hashContenu,
            typeDocument  : typeDocument,
            refDoc        : refDoc,
            horodatage    : block.timestamp,
            enregistrePar : msg.sender
        });

        _listeRefs.push(refDoc);

        emit DocumentEnregistre(refDoc, hashContenu, typeDocument, block.timestamp, msg.sender);
    }

    // ──────────────────────────────────────────────────────────────────
    // FONCTIONS DE LECTURE (gratuites — aucune transaction)
    // ──────────────────────────────────────────────────────────────────

    /**
     * @notice Vérifie si un document correspond à son empreinte enregistrée.
     * @return estValide  true si le hash correspond (document intact)
     */
    function verifierDocument(string memory refDoc, bytes32 hashAVerifier)
        external view returns (bool estValide)
    {
        return _documents[refDoc].hashContenu == hashAVerifier;
    }

    /**
     * @notice Retourne les informations complètes d'un document enregistré.
     */
    function getDocument(string memory refDoc)
        external view
        returns (
            bytes32 hashContenu,
            string  memory typeDocument,
            uint256 horodatage,
            address enregistrePar
        )
    {
        DocumentAncre memory doc = _documents[refDoc];
        return (doc.hashContenu, doc.typeDocument, doc.horodatage, doc.enregistrePar);
    }

    /**
     * @notice Retourne le nombre total de documents enregistrés.
     */
    function getNbDocuments() external view returns (uint256) {
        return _listeRefs.length;
    }

    /**
     * @notice Retourne la référence d'un document par son index.
     */
    function getRefParIndex(uint256 index) external view returns (string memory) {
        require(index < _listeRefs.length, "Index hors limites");
        return _listeRefs[index];
    }
}
