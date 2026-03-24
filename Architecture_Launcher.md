# 📄 Rapport de Conception Architecture : Déploiement "Zero-Touch" ERP

Ce rapport détaille de manière exhaustive les négociations, les contraintes techniques, et les solutions architecturales choisies pour la création du Launcher/Installeur de l'ERP (Projet PFC 2025-2026).

---

## 1. L'Origine du Besoin : Le Confort "Crème de la Crème"
L'objectif principal est de fournir une solution **On-Premise** (hébergée chez le client) avec l'expérience utilisateur d'un logiciel SaaS moderne. Le client doit pouvoir télécharger un fichier unique depuis le site vitrine, faire un double-clic, et avoir un ERP fonctionnel sans aucune intervention technique.

*   **Zéro-Expertise** : Le client ne doit pas configurer de bases de données, ni installer de dépendances (.NET, etc.), ni modifier des variables systèmes.
*   **Autonomie** : Le logiciel doit gérer ses propres mises à jour, ses prérequis et ses redémarrages de façon invisible.

---

## 2. Compilation et Emballage (Le Moteur)
Pour éviter que le client n'ait à deviner quelle version d'ASP.NET Core installer, nous avons opté pour le **Self-Contained Deployment (SCD)**.
*   **Fonctionnement** : La commande `dotnet publish` embarque le runtime .NET directement dans l'exécutable (`.exe`).
*   **Résultat** : Un exécutable autonome et indépendant de ce qui est déjà installé sur le PC du client.

---

## 3. Le Choix de l'Installeur : La bataille des Outils
Nous devions choisir un outil pour créer le fichier d'installation initial (le premier contact du client).

*   **Velopack (Le moderne)** : 
    *   *Avantage* : Met à jour le logiciel en arrière-plan (Over-The-Air) de façon transparente, comme Discord.
    *   *Inconvénient* : S'installe souvent dans l'espace Utilisateur (`AppData`) et manque de droits profonds pour créer des Services Windows durables.
*   **Inno Setup (Le robuste)** : 
    *   *Avantage* : Gratuit, extrêmement fiable, utilise des scripts Pascal (très puissants), gère parfaitement les droits Administrateur (UAC), configure le Pare-Feu, et crée les Services Windows. Il offre aussi un nettoyage agressif et parfait lors de la désinstallation.
    *   *Inconvénient* : Ne gère pas les mises à jour automatiques transparentes de base.
*   **L'Approche Choisie (Hybride)** : 
    Nous utiliserons **Inno Setup** pour le premier déploiement ("poser les fondations" et donner les gros privilèges systèmes), et nous intégrerons un système de mise à jour **intégré au code de l'ERP (un Watchdog/Bootstrapper)** pour la suite.

---

## 4. Le Dilemme de la Base de Données (Parité Dev/Prod)
Le choix de la base de données intégrée a été longuement discuté.

*   **SQLite** : Idéal pour du déploiement en un clic (un simple fichier), mais trop faible pour un ERP manipulé par des dizaines d'employés, et surtout, différent de l'environnement de développement (qui requiert un vrai moteur relationnel).
*   **SQL Server Express** : Trop lourd (300Mo), installeur lent, brise l'expérience "un clic".
*   **PostgreSQL (Le gagnant)** : C'est la base de données utilisée dans le développement de l'ERP. 
    *   *Le problème* : L'installeur officiel demande l'intervention humaine.
    *   *La solution choisie* : **PostgreSQL Portable**. Les binaires bruts de Postgres seront embarqués dans le Launcher. Au lancement de l'ERP, un script interne démarre le moteur PostgreSQL de manière fantôme (en arrière-plan) et le connecte à l'application. Le client a un vrai serveur Postgres sans jamais l'avoir vu s'installer.

---

## 5. L'Intelligence Artificielle de l'Installeur : Le Bootstrapper
Pour que l'ERP gère l'ordinateur à la place de l'humain, l'installeur principal agit comme un "Chef d'orchestre" intelligent.

*   **Phase de Scan (Détection)** : Avant d'installer, il scanne le Registre Windows (`HKEY_LOCAL_MACHINE`) et les variables `PATH` pour vérifier l'existence de dépendances critiques (ex: `wkhtmltopdf` pour les PDF, ou d'anciennes instances de `PostgreSQL`).
*   **Phase d'Action** : 
    *   Si présent : Il ne réinstalle pas, il passe son chemin.
    *   Si absent ou obsolète : Il extrait la ressource depuis ses propres archives "zippées" et l'installe de manière **Silencieuse** (Silent Install), sans déclencher de fenêtres surgissantes (Pop-ups).

---

## 6. Mises à Jour et Continuité de Service (Le Watchdog)
Le défi ultime était de mettre à jour un ERP qui tourne sur un serveur 24h/24 et 7j/7. S'il n'est jamais éteint manuellement, il ne se met jamais à jour.

*   **Le Faux Concept** : Nous avons clarifié qu'une mise à jour **ne redémarre jamais le serveur (la machine physique)** de l'entreprise.
*   **Le Watchdog (Chien de garde)** : C'est un micro-programme de quelques kilos crées en C# qui "surveille" l'ERP. C'est lui qui communique avec Internet.
*   **Le Workflow de Mise à jour (La micro-coupure)** :
    1.  Le Watchdog télécharge la nouvelle version de l'ERP en arrière-plan de jour.
    2.  Pendant la nuit (ex: 3h du matin) ou sur validation d'un admin, le Watchdog fige et éteint le processus Web de l'ERP.
    3.  Il effectue **obligatoirement** une sauvegarde (Backup SQL) de l'état de PostgreSQL.
    4.  Il remplace les fichiers `.dll`/`.exe` de l'ERP.
    5.  Il rallume l'ERP.
    *Toute l'opération dure moins de **5 secondes**, garantissant une continuité de service quasi-parfaite sans stress pour les employés.*

---

## 7. Fonctionnalités Confirmées : Self-Healing et Anti-Conflit de Port
Après discussion, deux fonctionnalités essentielles ont été ajoutées à l'architecture centrale du Launcher.

### 7A. Auto-Détection de Port (Anti-Conflit) 🚦
Un serveur d'entreprise peut héberger d'autres logiciels qui utilisent déjà le port réseau standard (ex: Port 5000). Sans protection, l'ERP se planterait au démarrage avec une erreur obscure.
*   **La solution** : Le Launcher, **avant même de copier le premier fichier**, écoute le réseau local. S'il détecte un conflit sur le port 5000, il bascule toute la configuration (raccourcis, pare-feu, fichiers de config) sur un port libre disponible (ex: 5001, 5002...) de manière **totalement automatique**. Le client ne voit jamais d'erreur.

### 7B. Self-Healing (Auto-Guérison) 🩺
Si l'ERP crashe en pleine journée (surcharge mémoire, bug applicatif, coupure momentanée...), sans Self-Healing, l'entreprise se retrouve bloquée jusqu'à ce qu'un administrateur intervienne manuellement.
*   **La solution** : Le Watchdog surveille l'état du processus ERP en temps réel. Dès qu'un crash est détecté (le processus Web est mort), le Watchdog :
    1.  Nettoie la mémoire et les fichiers temporaires.
    2.  Relance le service PostgreSQL si nécessaire.
    3.  Redémarre le serveur Web de l'ERP.
    *L'opération prend moins de **3 secondes** et est **transparente** pour les employés qui étaient sur une autre page.*

---

## 8. Outils Définitivement Sécurisés pour le Projet
1.  **Inno Setup** : Pour l'exécutable/Sfx de base (Setup.exe).
2.  **.NET 8/9 / C#** : Pour écrire le Bootstrapper et le Watchdog.
3.  **PostgreSQL (Binaires Portables)** : Pour la base de données intégrée furtivement.
4.  **Velopack / ou Script Custom** : Pour la partie "Téléchargement et application des patchs".
5.  **NSSM (Non-Sucking Service Manager) / API Windows** : Pour ancrer le logiciel en tant que *Service Windows* increvable et invisible.

---

## 9. Pistes d'Amélioration Futures (V2+)
Ces fonctionnalités ont été identifiées comme des améliorations "Ultra-Premium" à implémenter dans une version future du Launcher, une fois la V1 stable.

*   **Certificat SSL Automatisé (HTTPS sans avertissement)** 🔒 : L'installeur auto-génère un certificat de sécurité local de confiance. Le client voit un cadenas vert dès le premier lancement, sans message d'avertissement "site non sécurisé".

*   **Restauration "Time Machine" (En 1 clic)** ⏪ : Un bouton unique *"Restaurer l'ERP à la sauvegarde d'hier"* disponible dans le tableau de bord. En cas d'erreur critique dans la comptabilité ou les stocks, un administrateur peut revenir à l'état précédent en 5 secondes, sans aucune ligne de commande.

*   **Nom de domaine magique mDNS** 🌐 : Au lieu de donner aux employés une adresse IP (instable si le routeur redémarre), le Launcher configure le réseau local pour que l'ERP soit accessible à une URL fixe et conviviale, comme `http://erp-entreprise.local`.

*   **Intégration automatique du Tunnel Cloudflare** ☁️ : Pour les versions "Premium" de l'ERP, l'installeur peut directement configurer le tunnel Cloudflare afin de rendre l'ERP accessible depuis n'importe quel smartphone dans le monde, sans intervention technique.

---

**Fin du rapport.**
