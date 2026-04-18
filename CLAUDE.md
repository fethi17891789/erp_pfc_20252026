# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

# Briefing Complet du Projet ERP SKYRA

## 🎯 Objectif du Projet

Ce projet est un **ERP (Enterprise Resource Planning) complet**, développé dans le cadre d'un **Projet de Fin de Cycle (PFC) universitaire 2025-2026**. L'objectif est de construire un ERP **modulaire, moderne, autosuffisant et visuellement premium**, capable de rivaliser avec les interfaces des ERP commerciaux.

Le nom marketing du produit est **« SKYRA »** (anciennement « ERP Suite »). L'application est conçue pour être déployée en **one-click** via un installateur Windows (Inno Setup) et fonctionne comme une application desktop autonome qui lance un serveur web local.

**Ce n'est PAS un MVP simple.** C'est un ERP à vocation professionnelle avec une esthétique premium, des animations fluides, du glassmorphism, et un design sombre/violet très soigné.

---

## 🧭 PHILOSOPHIE UX — RÈGLE FONDAMENTALE

> **SKYRA doit être aussi puissant qu'Odoo, mais significativement plus simple à utiliser.**

Odoo est réputé pour sa richesse fonctionnelle mais souffre d'une complexité excessive qui décourage les utilisateurs. SKYRA vise à offrir autant (voire plus) de fonctionnalités tout en restant **intuitif dès la première utilisation**.

### Principes UX à respecter impérativement dans tout développement :

1. **0-click pour les tâches fréquentes** — Les valeurs par défaut doivent être intelligentes. L'utilisateur ne doit pas avoir à configurer ce qui est évident.

2. **Zéro jargon ERP sans explication** — Les labels doivent être compréhensibles sans formation. Si un terme technique est inévitable (BOM, MRP, OF), il doit être accompagné d'une explication courte en sous-texte ou tooltip.

3. **Feedback immédiat et visible** — Chaque action doit produire un retour visuel dans les 300ms : toast de succès/erreur, spinner de chargement, état de bouton "en cours". Utiliser systématiquement des toasts animés (pattern déjà en place sur ProduitNew), jamais `alert()` ou `confirm()` natif du navigateur.

4. **États vides utiles (Empty States)** — Une page vide n'est jamais une impasse. Elle doit expliquer quoi faire et proposer un CTA clair (bouton "Créer votre premier X").

5. **L'IA suggère proactivement** — L'assistant Gemini intégré doit être accessible depuis n'importe quel module pour guider l'utilisateur ("Comment créer une BOM ?", "Explique-moi le MRP").

6. **Cohérence absolue des patterns** — Un même type d'action doit toujours se faire de la même façon dans tous les modules : même style de modale de suppression (jamais `confirm()` natif), même style de toast, même position des boutons d'action.

7. **Navigation contextuelle** — La sidebar doit toujours indiquer où on est. Le sous-module actif doit automatiquement ouvrir son accordéon parent. Un fil d'Ariane (breadcrumb) doit apparaître sur les pages de détail.

8. **Simplicité ≠ moins de fonctionnalités** — Ne jamais supprimer une fonctionnalité au nom de la simplicité. Simplifier = mieux organiser, mieux présenter, mieux guider. Les options avancées peuvent être dans des sections "Avancé" collapsibles.

---

## 📐 Architecture Technique

### Stack Principal

| Composant | Technologie |
|---|---|
| **Backend** | ASP.NET Core 8.0 (Razor Pages) |
| **Frontend** | HTML/CSS/JS natif (pas de framework JS) |
| **Base de données** | PostgreSQL (via Npgsql + Entity Framework Core) |
| **ORM** | Entity Framework Core 8.0 |
| **Temps réel** | SignalR (ChatHub + LogistiqueHub) |
| **PDF** | wkhtmltopdf (via Haukcode.WkHtmlToPdfDotNet) |
| **IA** | Google Gemini API (gratuit via clé API) |
| **Cartographie** | Mapbox GL JS (token gratuit) |
| **Polices** | Google Fonts (Plus Jakarta Sans) |
| **Déploiement** | Inno Setup (installateur Windows) |

### Structure des Dossiers

```
erp pfc 20252026/
├── Program.cs                    # Point d'entrée — config DI, création auto des tables, routes
├── appsettings.json              # Config (connexion BDD, Mapbox token)
├── erp pfc 20252026.csproj       # Projet .NET 8
│
├── Donnees/                      # 📦 Couche DATA (Entités + DbContext)
│   ├── erpDBcontext.cs           #   DbContext EF Core (toutes les entités)
│   ├── Donnees/ErpUser.cs        #   Entité utilisateur ERP
│   ├── Produit.cs                #   Entité Produit (biens/services)
│   ├── Bom.cs                    #   Entité Nomenclature (Bill of Materials)
│   ├── Contact.cs                #   Entité Contact CRM
│   ├── ContactRelation.cs        #   Relations entre contacts
│   ├── Conversation.cs           #   Entité conversation messagerie
│   ├── Message.cs                #   Entité message
│   ├── MessageAttachment.cs      #   Pièces jointes messages
│   ├── MessageReadState.cs       #   État lecture messages
│   ├── MRPPlan.cs                #   Plan MRP
│   ├── MRPPlanLigne.cs           #   Ligne d'un plan MRP
│   ├── MRPTableau.cs             #   Détail périodique MRP
│   ├── MRPFichier.cs             #   Fichiers PDF (OF) stockés en base
│   ├── MRPConfigModule.cs        #   Configuration du module MRP
│   ├── IaConfiguration.cs        #   Configuration IA (provider, clé, modèle)
│   ├── DynamicConnectionProvider.cs  # Connexion BDD dynamique
│   ├── ErpConfigStorage.cs       #   Stockage config ERP (erpconfig.json)
│   └── Logistique/               #   Entités logistique
│       ├── Vehicule.cs
│       ├── Capteur.cs
│       └── Trajet.cs
│
├── métiers/                      # 🔧 Couche MÉTIER (Services)
│   ├── BDDService.cs             #   Service global BDD
│   ├── IAService.cs              #   Service IA Gemini (chat, JSON structuré, streaming SSE)
│   ├── PdfService.cs             #   Génération de PDF
│   ├── CRM/
│   │   ├── AnnuaireService.cs    #   Service annuaire contacts
│   │   └── ValidationService.cs  #   Validation + enrichissement IA des contacts
│   ├── MRP/
│   │   ├── MRPConfigService.cs   #   Config du module MRP
│   │   ├── OrdreFabricationService.cs  # Ordres de fabrication
│   │   └── OrdreAchatService.cs  #   Ordres d'achat
│   ├── Messagerie/
│   │   ├── MessagerieService.cs  #   Service de messagerie complet
│   │   ├── ChatHub.cs            #   Hub SignalR (temps réel, WebRTC, IA)
│   │   └── ChatMessageDto.cs     #   DTO messages
│   └── Logistique/
│       ├── LogistiqueService.cs  #   CRUD véhicules/capteurs/trajets
│       └── LogistiqueHub.cs      #   Hub SignalR tracking temps réel
│
├── Presentation/                 # 🖥️ Couche PRÉSENTATION
│   ├── controller/
│   │   └── BDDController.cs
│   └── view/                     #   Razor Pages
│       ├── Shared/_Layout.cshtml #   ★ Layout global (sidebar, header, notifications)
│       ├── Home.cshtml           #   Dashboard d'accueil
│       ├── Login.cshtml          #   Page de connexion
│       ├── ChooseProfile.cshtml  #   Sélection de profil
│       ├── CreateProfile.cshtml  #   Création de profil employé
│       ├── BDDView.cshtml        #   Configuration BDD (première connexion)
│       ├── ProduitCreate.cshtml  #   CRUD Produits
│       ├── ProduitNew.cshtml     #   Formulaire nouveau produit
│       ├── BOM.cshtml            #   Liste des nomenclatures
│       ├── BOMCreate.cshtml      #   Création/édition de nomenclature
│       ├── MRP.cshtml            #   Liste des planifications MRP
│       ├── MRPDetail.cshtml      #   ★ Détail MRP (tableau, OF, OA, PDF)
│       ├── MRPConfig.cshtml      #   Configuration module MRP
│       ├── Fabrication.cshtml    #   Hub fabrication
│       ├── Messagerie.cshtml     #   ★ Messagerie temps réel complète
│       ├── AnnuaireList.cshtml   #   Liste contacts CRM
│       ├── AnnuaireNew.cshtml    #   ★ Fiche contact (enrichissement IA)
│       ├── Reports.cshtml        #   Module rapports
│       ├── Logistique/
│       │   ├── Index.cshtml      #   ★ Dashboard logistique (carte temps réel)
│       │   └── Tracking.cshtml   #   ★ Suivi GPS avancé
│       └── Settings/
│           └── MobileAccess.cshtml  # Accès mobile (QR Code)
│
├── wwwroot/                      # 📁 Fichiers statiques
│   ├── css/
│   │   ├── Common.css            #   ★ Design system global (variables, tokens, composants)
│   │   ├── Home.css              #   Styles dashboard + layout sidebar/header
│   │   └── ...
│   ├── js/
│   │   ├── Messagerie.js         #   ★ Logique messagerie (55KB — très complexe)
│   │   ├── Notifications.js      #   Système de notifications temps réel
│   │   ├── Presence.js           #   Détection présence utilisateurs (online/offline)
│   │   └── ...
│   ├── images/                   #   Assets visuels (logos, avatars, produits)
│   └── uploads/                  #   Fichiers uploadés par les utilisateurs
│
└── Launcher/                     # 🚀 Système de déploiement
    ├── ERP.Bootstrapper/         #   Installateur automatique (PostgreSQL, .NET Runtime)
    ├── ERP.Watchdog/             #   Service de surveillance (auto-restart + auto-update)
    └── InnoSetup/
        └── SKYRA_Setup.iss       #   Script Inno Setup (installateur Windows)
```

---

## 🧩 Modules Fonctionnels Existants

### 1. 🏠 Accueil (Dashboard)
- Page d'accueil avec vue d'ensemble de l'ERP
- Navigation par sidebar collapsible avec accordéons
- Design premium dark/violet avec glassmorphism

### 2. 🏭 Fabrication / Production
- **Produits** : CRUD complet (biens/services), images, codes-barres, coûts détaillés (achat, BOM, autres charges)
- **BOM (Bill of Materials)** : Nomenclatures multi-niveaux, calcul automatique des coûts, liaison composants
- **MRP (Material Requirements Planning)** : Planification des besoins matières, horizons configurables, tableau périodique MRP complet (besoins bruts/nets, stock prévisionnel, ordres planifiés)
- **Ordres de Fabrication (OF)** : Génération automatique, export PDF stocké en base (BYTEA)
- **Ordres d'Achat (OA)** : Génération pour les matières premières, envoi via messagerie interne

### 3. 💬 Messagerie (Temps Réel)
- Chat temps réel via SignalR
- Conversations directes et de groupe
- Pièces jointes (images, fichiers)
- Envoi d'Ordres d'Achat (OA) en tant que messages interactifs
- Intégration IA (GEMINI) : assistant conversationnel avec streaming SSE
- Appels WebRTC (audio/vidéo) entre utilisateurs
- Indicateurs de lecture, édition, suppression de messages
- Système de notifications iOS-style avec toast animé

### 4. 📍 Logistique & Tracking
- Dashboard carte temps réel (Mapbox GL JS)
- Gestion de flotte : véhicules, capteurs IoT, trajets
- Suivi GPS en temps réel via SignalR (LogistiqueHub)
- Historique des trajets avec trace JSON
- Page de tracking avancé avec replay de trajets

### 5. 📇 CRM / Annuaire
- Gestion de contacts (clients, fournisseurs, partenaires)
- Enrichissement IA automatique via Google Gemini (recherche web OSINT)
- Validation intelligente (email, téléphone via libphonenumber, DNS MX)
- Relations entre contacts (graphe relationnel)
- Avatars et fiches détaillées

### 6. 🤖 Module IA (GEMINI)
- Intégration Google Gemini API (provider configurable)
- Découverte dynamique des modèles disponibles (classement automatique)
- Failover automatique entre modèles (Pro → Flash, versions 2.5 → 2.0 → 1.5)
- Streaming SSE pour les réponses en temps réel
- Recherche Google Search intégrée via les outils Gemini
- RAG basique : injection de contexte ERP (stats produits, utilisateurs, véhicules)
- Configuration en base (table IaConfiguration)

### 7. 👤 Gestion des Profils / Utilisateurs
- Création de profils avec poste, avatar, email
- Système de sessions (.erp_pfc_20252026.session)
- Détection de présence en ligne/hors ligne (SignalR)
- Sélection de profil au démarrage

### 8. 📊 Rapports
- Module de reporting (en développement)
- Export PDF via wkhtmltopdf

### 9. ⚙️ Paramètres
- Accès mobile via QR Code
- Configuration IA (clé API, modèle, prompt système)
- Configuration MRP (horizon par défaut)

---

## 🎨 Design System & Charte Graphique

### Palette de Couleurs
```css
--bg: #02030A          /* Fond principal (noir profond) */
--bg-alt: #050718      /* Fond alternatif */
--text: #FFFFFF        /* Texte principal */
--text-muted: #A4A7C8  /* Texte secondaire */
--text-muted-2: #7F83A5 /* Texte tertiaire */
--accent: #7B5EFF      /* ★ Accent principal (violet) */
--accent-hover: #8B6FFF /* Accent hover */
--accent-light: #9C8CFF /* Accent clair */
--accent-glow: rgba(123, 94, 255, 0.25) /* Glow violet */
```

### Principes de Design
- **Thème** : Dark mode exclusif, fond quasi-noir avec nuances de violet
- **Glassmorphism** : Bordures semi-transparentes, backdrop-filter blur(20px)
- **Animations** : Transitions fluides (cubic-bezier), micro-animations, effets de hover premium
- **Typography** : Plus Jakarta Sans (Google Fonts), poids 400-800
- **Radius** : Coins très arrondis (12px → 32px → 999px pour les pills)
- **Gradients** : Radiaux subtils en fond, linéaires sur les boutons
- **Shadows** : Ombres violettes diffuses sur les éléments interactifs

### Règles CSS Critiques
1. **JAMAIS de scroll global** — `body { overflow: hidden; height: 100vh; }`
2. Le scroll est géré localement par zone (`.erp-main`, listes, etc.)
3. Le fichier `Common.css` est le design system partagé par TOUTES les pages
4. Le fichier `Home.css` contient les styles du layout (sidebar, header, main)
5. Chaque module peut avoir son propre CSS additionnel

---

## 🔗 Routes & Navigation

| Route | Page | Description |
|---|---|---|
| `/` | BDDView | Configuration BDD (première utilisation) |
| `/Login` | Login | Connexion utilisateur |
| `/ChooseProfile` | ChooseProfile | Sélection de profil |
| `/CreateProfile` | CreateProfile | Création de profil |
| `/Home` | Home | Dashboard principal |
| `/ProduitCreate` | ProduitCreate | Liste/CRUD Produits |
| `/ProduitNew` | ProduitNew | Formulaire produit |
| `/BOM` | BOM | Liste nomenclatures |
| `/BOMCreate` | BOMCreate | Création nomenclature |
| `/MRP` | MRP | Liste planifications MRP |
| `/MRPDetail` | MRPDetail | Détail d'une planification |
| `/MRPConfig` | MRPConfig | Configuration MRP |
| `/Fabrication` | Fabrication | Hub fabrication |
| `/Messagerie` | Messagerie | Messagerie temps réel |
| `/AnnuaireList` | AnnuaireList | Liste contacts CRM |
| `/AnnuaireNew` | AnnuaireNew | Fiche contact |
| `/Logistique/Index` | Logistique.Index | Dashboard logistique |
| `/Logistique/Tracking` | Logistique.Tracking | Tracking GPS |
| `/Reports` | Reports | Rapports |
| `/Settings/MobileAccess` | MobileAccess | Accès mobile |

### Hubs SignalR
- `/chathub` — Messagerie temps réel + IA + WebRTC
- `/logistiquehub` — Tracking véhicules temps réel

---

## ⚠️ CONTRAINTES CRITIQUES DE DÉVELOPPEMENT

### 1. 🆓 ZÉRO COÛT — Outils Gratuits UNIQUEMENT

> **C'est la contrainte la plus importante du projet.**

- **INTERDIT** d'utiliser des APIs payantes (pas d'OpenAI payant, pas de services cloud facturés)
- **INTERDIT** d'utiliser des bibliothèques ou services avec licence commerciale
- Toute fonctionnalité doit reposer sur des outils **100% gratuits ou open source**
- L'IA utilise **Google Gemini API** (tier gratuit avec clé API gratuite)
- La cartographie utilise **Mapbox** (tier gratuit)
- Si un service gratuit a des limites (quotas), implémenter un **fallback** ou un mode dégradé

### 2. 🏗️ Conventions de Code

- **Langue du code** : Les noms de variables, classes et méthodes sont en **français** (ex: `ProduitCreate`, `OrdreFabricationService`, `QuantiteBesoin`)
- **Langue des commentaires** : Français
- **Namespaces** :
  - `Donnees` pour la couche data
  - `Metier` pour la couche métier
  - `Donnees.Logistique` pour les entités logistique
  - `Metier.CRM`, `Metier.MRP`, `Metier.Messagerie`, `Metier.Logistique` pour les services
- **Pas de migrations EF Core** : Les tables sont créées automatiquement via des scripts SQL bruts dans `Program.cs` au démarrage
- **Pattern** : Code-behind Razor Pages (`.cshtml` + `.cshtml.cs`)

### 3. 🎨 Exigences Design

- Toute nouvelle page DOIT utiliser le design system de `Common.css` et `Home.css`
- Toute nouvelle page DOIT utiliser le `_Layout.cshtml` partagé (sidebar + header)
- Le thème est **dark mode exclusif** — JAMAIS de fond blanc ou de design "light"
- Les couleurs d'accent sont les **violets** (#7B5EFF, #8B6FFF, #9C8CFF)
- Les animations et transitions sont **obligatoires** pour une UX premium
- Les bordures utilisent `rgba(255, 255, 255, 0.08)` — jamais de borders solides

### 4. 🗃️ Base de Données

- La connexion BDD est **dynamique** (via `DynamicConnectionProvider`)
- Les tables sont auto-créées au démarrage dans `Program.cs` (pattern "CREATE TABLE IF NOT EXISTS")
- Toute nouvelle table doit suivre ce pattern d'auto-création
- Les colonnes de timestamp utilisent `TIMESTAMP WITHOUT TIME ZONE` sauf pour les plans MRP qui utilisent `TIMESTAMP WITH TIME ZONE`
- Les noms de tables et colonnes PostgreSQL sont entre **guillemets doubles** (sensible à la casse)

### 5. 🔐 Authentification

- Système de sessions simple (pas d'Identity, pas de JWT)
- L'ID utilisateur est stocké en session : `Session.GetInt32("CurrentUserId")`
- Le login est stocké en session : `Session.GetString("CurrentUserLogin")`
- Pas de système de rôles/permissions implémenté

---

## 🚀 Modules en Cours de Développement / Prévus

Les modules suivants sont mentionnés mais pas encore implémentés :
- **Ventes** (facturation, devis, commandes clients)
- **Achats** (commandes fournisseurs, réception)
- **Stock / Inventaire** (mouvements, alertes de stock)
- **RH** (gestion des employés, présence, congés)
- **Comptabilité** (journal, bilan, grand livre)

---

## 🔧 Comment Lancer le Projet

```bash
# 1. S'assurer que PostgreSQL est en cours d'exécution (port 5432)
# 2. La BDD par défaut est "fethifethifethi" avec user "openpg" / password "openpgpwd"
# 3. Lancer l'application :
dotnet run
# L'app démarre sur http://localhost:5000 et ouvre automatiquement le navigateur

# Build
dotnet build

# Publish self-contained (pour packaging avec Inno Setup)
dotnet publish -c Release --self-contained

# EF Core (rarement utilisé — les tables sont créées via SQL dans Program.cs)
dotnet ef migrations add <NomMigration>
dotnet ef database update
```

---

## 📝 Notes Importantes

1. **Le fichier `Program.cs` est MASSIF (~900 lignes)** — il contient toute la configuration DI, la création automatique de toutes les tables, et le pipeline HTTP. C'est voulu : pas de migrations EF, tout est auto-géré au démarrage.

2. **Le fichier `Messagerie.js` est très complexe (~55KB)** — il gère le chat temps réel, les appels WebRTC, l'intégration IA, les pièces jointes, les OA interactifs, les emojis, etc. Toute modification doit être faite avec précaution.

3. **Le `_Layout.cshtml` est le cœur de la navigation** — sidebar, header, notifications, présence, logout modal. Toute nouvelle page hérite de ce layout.

4. **L'IA (IAService.cs) implémente un failover dynamique** — elle découvre automatiquement les modèles Gemini disponibles et bascule en cas d'erreur. Ne pas hardcoder de nom de modèle.

5. **Le Launcher (Bootstrapper + Watchdog + InnoSetup)** est un projet séparé dans le même repo. Il ne fait PAS partie du build principal (.csproj l'exclut explicitement).

6. **Le projet utilise des accents dans les noms de dossiers** (`métiers/`) — attention aux chemins sur certains OS/outils.
