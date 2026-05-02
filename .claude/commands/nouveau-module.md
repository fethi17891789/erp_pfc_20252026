Tu vas m'aider à concevoir un nouveau module pour SKYRA ERP. Le module concerné est : $ARGUMENTS

Réponds en deux parties distinctes.

---

## PARTIE 1 — INSPIRATION & IDÉES

Propose entre 5 et 8 idées de fonctionnalités pour ce module. Mélange :
- Des fonctionnalités simples et attendues (que tout bon ERP devrait avoir)
- Au moins 2 idées innovantes qu'aucun ERP concurrent (Odoo, SAP, Dynamics, Sage) ne propose, ou que très peu proposent

Pour chaque idée, explique-la comme si tu parlais à quelqu'un qui n'a jamais utilisé d'ERP. Pas de jargon. Une phrase simple qui dit ce que ça fait concrètement pour l'utilisateur.

Indique clairement avec une pastille 🚀 les idées innovantes absentes des ERPs concurrents.

Ensuite, propose un workflow en 3 à 5 étapes maximum, rédigé simplement :
"L'utilisateur fait X → SKYRA fait Y automatiquement → L'utilisateur voit Z"
Le workflow doit être évident, sans formation nécessaire. Pense à ce qu'Odoo fait en 12 clics et propose-le en 3.

---

## PARTIE 2 — CAHIER DES CHARGES

Pose-moi les questions suivantes une section à la fois, en attendant mes réponses avant de passer à la suivante :

**1. PÉRIMÈTRE**
Quels sont les objets principaux de ce module ? (ex : devis, facture, commande…)
Y a-t-il des objets dont tu ne veux PAS dans un premier temps ?

**2. DONNÉES**
Pour chaque objet, quels sont les champs indispensables ?
Quels modules SKYRA existants doivent être connectés ? (Contacts, Produits, MRP, Messagerie, Logistique…)

**3. ACTIONS UTILISATEUR**
Quel est le parcours principal de l'utilisateur dans ce module ?
Y a-t-il des statuts, des validations, des étapes d'approbation ?

**4. EXPORTS & AUTOMATISATIONS**
Faut-il générer des PDFs ? Lesquels ?
Des envois automatiques via la Messagerie interne ?
Des notifications ou alertes ?

**5. CONTRAINTES SPÉCIFIQUES**
Numérotation automatique (ex : DEV-2025-001) ?
Règles métier particulières (remises, taxes, délais…) ?
Quelque chose qu'aucun ERP ne fait et que tu voudrais intégrer ici ?

Une fois toutes mes réponses reçues, génère un document de cahier des charges complet et sauvegarde-le dans `.claude/specs/$ARGUMENTS.md`.
