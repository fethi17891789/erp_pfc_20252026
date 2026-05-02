# Cahier des Charges — Module Achats SKYRA

---

## 1. PÉRIMÈTRE

### Documents gérés
| Document | Code | Description |
|---|---|---|
| Bon de Commande | BC-2025-001 | Document officiel envoyé au fournisseur |
| Proforma fournisseur | — | Réponse du fournisseur (stockée, pas générée) |
| Bon de Réception | BR-2025-001 | Enregistrement de la réception physique |
| Facture fournisseur | FAC-2025-001 | Rapprochement comptable |

**Exclu de la V1 :** Demande d'achat interne

### Workflow complet
```
BESOIN (MRP/OA)
     ↓
BON DE COMMANDE (BC)         → PDF généré + envoi mail fournisseur
     ↓
PROFORMA FOURNISSEUR         → Stocké dans SKYRA
     ↓
CONFIRMATION BC               → Via interface email (sans ERP)
     ↓
BON DE RÉCEPTION (BR)        → Mise à jour quantité disponible produit
     ↓
FACTURE FOURNISSEUR          → Rapprochement BC/BR/Facture
```

---

## 2. DONNÉES

### Bon de Commande (BC)
- Numéro automatique : `BC-AAAA-NNN`
- Fournisseur (Annuaire — type fournisseur uniquement)
- Date de commande
- Date de livraison souhaitée
- Lignes : composant, quantité, prix unitaire HT, total HT
- Sous-total HT, TVA 19%, Total TTC
- Statut : Brouillon → Envoyé → Confirmé → Partiellement reçu → Reçu → Facturé → Refusé
- Notes / conditions particulières
- Token unique (pour interface email fournisseur)
- PDF généré (stocké en BYTEA) + date d'envoi mail

### Proforma fournisseur
- Lié au BC
- Date de réception
- Montant HT proposé par le fournisseur
- Fichier PDF uploadé (optionnel)
- Notes

### Bon de Réception (BR)
- Numéro automatique : `BR-AAAA-NNN`
- Lié au BC
- Date de réception
- Lignes : composant, quantité commandée, quantité reçue, état (conforme / endommagé)
- Validation → mise à jour automatique quantité disponible sur fiche produit

### Facture fournisseur
- Numéro automatique : `FAC-AAAA-NNN`
- Numéro facture fournisseur (saisi manuellement)
- Liée au BC + BR
- Montant HT, TVA 19%, TTC
- Alerte si écart > 2% avec le BC (non bloquant)
- Statut : Reçue → Vérifiée → Comptabilisée

### Historique des prix
- Par composant + par fournisseur
- Date, prix unitaire HT, quantité commandée
- Graphique 12 mois : courbe par fournisseur + ligne pointillée "prix BOM actuel"
- Filtres cliquables par fournisseur

### Configuration module (premier lancement)
- Politique de prix : **Option A** (dernier prix d'achat) ou **Option B** (moyenne pondérée)
- Prix de référence fournisseur principal conservé séparément

### Score fiabilité fournisseur (calculé automatiquement)
- Respect des délais (date livraison souhaitée vs réelle)
- Stabilité des prix (variance sur 12 mois)
- Qualité des livraisons (quantités reçues vs commandées, état)
- Affiché en couleur : vert / orange / rouge

---

## 3. ACTIONS UTILISATEUR

### Parcours principal
1. Créer un BC (depuis MRP/OA ou manuellement)
2. Vérifier les lignes, prix, fournisseur
3. Envoyer au fournisseur par mail (PDF joint + lien confirmation)
4. Fournisseur confirme/refuse via lien email (sans login ERP)
5. Si confirmé → créer le BR à la réception physique
6. Valider le BR → quantité disponible mise à jour
7. Enregistrer la facture fournisseur + rapprocher avec BC

### Gestion sous-traitance
- Si un composant commandé n'est pas de type "matière première" → popup d'avertissement "Ce composant est en sous-traitance" + confirmation requise

### Comportement si refus fournisseur
- BC passe au statut "Refusé"
- Notification dans la Messagerie interne
- KPI card Home "Commandes refusées" incrémentée
- Possibilité de dupliquer le BC pour relancer avec un autre fournisseur

---

## 4. EXPORTS & AUTOMATISATIONS

### PDFs générés
- Bon de Commande (envoyé au fournisseur)
- Bon de Réception (interne)
- Facture fournisseur (format récapitulatif)
- Tous incluent : logo SKYRA, numérotation, HT + TVA 19% + TTC

### Envoi email fournisseur
- Email avec BC en pièce jointe
- Lien unique (token) vers page de confirmation publique
- Page légère sans login : affiche le BC + boutons Confirmer / Refuser / Proposer une date alternative
- Réponse mise à jour automatiquement dans SKYRA

### Notifications & alertes
- Messagerie interne : BC confirmé / refusé / livraison en retard
- Alerte orange non bloquante : prix > historique habituel
- Alerte si écart facture vs BC > 2%

---

## 5. CONTRAINTES SPÉCIFIQUES

- Devise : DZD uniquement (multi-devise prévu en V2)
- TVA : 19% fixe, non configurable
- Numérotation : automatique, séquentielle par année
- Aucune validation manager requise — tout utilisateur peut créer et envoyer un BC
- Annuaire : seuls les contacts de type "Fournisseur" apparaissent dans la sélection
- Simulateur "Et si ?" : impact d'une hausse de X% sur toutes les BOMs liées
- Politique de prix : choix au premier lancement du module (configurable en paramètres ensuite)

---

## 6. KPI CARDS HOME (nouvelles)

| KPI | Condition d'affichage |
|---|---|
| OA non répondus | OA envoyés sans réponse (existant) |
| OA acceptés non amorcés | OA acceptés, BC pas encore créé (existant) |
| Commandes en cours | BC envoyés, confirmation en attente |
| Commandes refusées | BC refusés par le fournisseur |

---

## 7. DESIGN & UX

- Inspiré du module Fabrication (cartes visuelles, pas de tableaux austères)
- Timeline visuelle du workflow sur chaque BC (étapes colorées)
- Vue liste : cards avec statut coloré, fournisseur, montant TTC, date livraison
- Graphique prix : directement sur la fiche composant (pas dans un rapport)
- Score fournisseur : pastille colorée visible partout où le fournisseur apparaît
- Politique des toasts SKYRA pour tous les feedbacks (jamais alert/confirm natif)
- Dark mode violet, glassmorphism, animations fluides
