// Fichier : wwwroot/js/CreateProfile.js
// Gestion du menu déroulant filtrable des postes

(function () {
    'use strict';

    const input = document.getElementById('job-input');
    const dropdown = document.getElementById('job-dropdown');
    const hiddenSelected = document.getElementById('SelectedPoste');

    if (!input || !dropdown || !hiddenSelected) {
        console.warn('CreateProfile.js: Éléments HTML manquants');
        return;
    }

    console.log('CreateProfile.js chargé correctement');

    // Liste de postes (≈ 220 intitulés réels)
    const jobPositions = [
        // Direction générale
        'PDG',
        'Directeur Général',
        'Directeur des Opérations',
        'Directeur Industriel',
        'Directeur de Site',
        'Directeur d\'Usine',
        'Directeur de Production',
        'Directeur Technique',
        'Directeur Supply Chain',
        'Directeur Logistique',
        'Directeur Achats',
        'Directeur Commercial',
        'Directeur Marketing',
        'Directeur Export',
        'Directeur Administratif et Financier (DAF)',
        'Directeur des Systèmes d\'Information (DSI)',
        'Directeur des Ressources Humaines (DRH)',
        'Directeur Qualité',
        'Directeur QHSE',
        'Directeur R&D',

        // Management / middle management
        'Responsable de Production',
        'Responsable d\'Atelier',
        'Responsable d\'UAP',
        'Responsable Maintenance',
        'Responsable Méthodes',
        'Responsable Amélioration Continue',
        'Responsable Supply Chain',
        'Responsable Planification',
        'Responsable Ordonnancement',
        'Responsable Logistique',
        'Responsable Entrepôt',
        'Responsable Transport',
        'Responsable Approvisionnement',
        'Responsable Achats',
        'Responsable Import-Export',
        'Responsable Qualité',
        'Responsable QSE',
        'Responsable HSE',
        'Responsable Projets',
        'Responsable Industrialisation',
        'Responsable Bureau d\'Études',
        'Responsable Informatique',
        'Responsable Infrastructure IT',
        'Responsable Applications Métier',
        'Responsable Sécurité SI',
        'Responsable Financier',
        'Responsable Contrôle de Gestion',
        'Responsable Comptable',
        'Responsable Trésorerie',
        'Responsable Paie',
        'Responsable Formation',
        'Responsable Recrutement',
        'Responsable Administration du Personnel',
        'Responsable Relations Sociales',
        'Responsable Service Client',
        'Responsable ADV (Administration des Ventes)',
        'Responsable Magasin',
        'Responsable Stocks',
        'Responsable Planning',
        'Responsable Ordonnancement-Lancement',
        'Responsable Lean Manufacturing',

        // Production / méthodes / industrialisation
        'Ingénieur de Production',
        'Ingénieur Méthodes',
        'Ingénieur Process',
        'Ingénieur Industrialisation',
        'Ingénieur Amélioration Continue',
        'Ingénieur Lean',
        'Ingénieur Qualité Production',
        'Ingénieur Maintenance',
        'Ingénieur Fiabilité',
        'Ingénieur Planning',
        'Ingénieur Supply Chain',
        'Ingénieur Logistique',
        'Ingénieur Ordonnancement',
        'Ingénieur Sécurité',
        'Ingénieur HSE',
        'Ingénieur Projets',
        'Ingénieur R&D',
        'Ingénieur Bureau d\'Études',
        'Ingénieur Études Mécaniques',
        'Ingénieur Études Électriques',
        'Chef de Production',
        'Chef d\'Atelier',
        'Chef d\'Équipe Production',
        'Chef de Ligne',
        'Animateur d\'Équipe',
        'Technicien Méthodes',
        'Technicien Industrialisation',
        'Technicien Process',
        'Technicien Ordonnancement',
        'Technicien Planning',
        'Technicien Qualité Production',
        'Technicien QHSE',
        'Technicien Contrôle Non Destructif',
        'Technicien Laboratoire',
        'Conducteur de Ligne',
        'Opérateur Machine',
        'Opérateur de Production',
        'Opérateur Polyvalent',
        'Régleur',
        'Régleur CNC',
        'Monteur-Opérateur',
        'Agent de Fabrication',
        'Agent de Conditionnement',

        // Maintenance / technique
        'Technicien de Maintenance',
        'Technicien de Maintenance Industrielle',
        'Technicien de Maintenance Électrique',
        'Technicien de Maintenance Mécanique',
        'Technicien de Maintenance Automatismes',
        'Électromécanicien',
        'Automaticien',
        'Ingénieur Automatismes',
        'Technicien Facility Management',
        'Chef d\'Équipe Maintenance',
        'Planificateur de Maintenance',
        'Coordinateur Maintenance',
        'Technicien SAV',
        'Technicien Itinérant',

        // Qualité / QHSE
        'Contrôleur Qualité',
        'Technicien Qualité',
        'Ingénieur Qualité Fournisseurs',
        'Ingénieur Qualité Clients',
        'Auditeur Qualité',
        'Responsable Qualité Système',
        'Coordinateur QHSE',
        'Animateur QSE',
        'Technicien HSE',
        'Chargé de Mission HSE',

        // Supply chain / logistique
        'Responsable MRP',
        'Planificateur de Production',
        'Planificateur Supply Chain',
        'Prévisionniste des Ventes',
        'Ordonnanceur',
        'Gestionnaire de Stocks',
        'Gestionnaire Approvisionnements',
        'Approvisionneur',
        'Acheteur',
        'Acheteur Industriel',
        'Acheteur Projet',
        'Acheteur Logistique',
        'Acheteur Transport',
        'Demand Planner',
        'Supply Planner',
        'Logisticien',
        'Chef Logistique',
        'Technicien Logistique',
        'Coordinateur Logistique',
        'Exploitant Transport',
        'Affréteur',
        'Chef d\'Entrepôt',
        'Chef de Quai',
        'Agent de Quai',
        'Cariste',
        'Magasinier',
        'Préparateur de Commandes',
        'Gestionnaire Flotte',
        'Responsable Douanes',
        'Agent d\'Exploitation Transport',
        'Responsable Distribution',
        'Responsable Plateforme Logistique',

        // Finance / comptabilité / contrôle de gestion
        'Comptable',
        'Comptable Client',
        'Comptable Fournisseur',
        'Comptable Général',
        'Assistant Comptable',
        'Chef Comptable',
        'Contrôleur de Gestion',
        'Contrôleur de Gestion Industriel',
        'Contrôleur de Gestion Commercial',
        'Auditeur Interne',
        'Analyste Financier',
        'Trésorier',
        'Gestionnaire de Trésorerie',
        'Responsable Crédit Client',
        'Gestionnaire Facturation',
        'Gestionnaire Recouvrement',
        'Analyste de Coûts',
        'Analyste Budget',

        // RH / paie
        'Responsable RH',
        'Chargé de Recrutement',
        'Chargé des Ressources Humaines',
        'Gestionnaire RH',
        'Gestionnaire Paie',
        'Technicien Paie',
        'Assistant RH',
        'Assistant Formation',
        'Assistant Recrutement',
        'Responsable Développement RH',
        'Responsable Formation',
        'Responsable Mobilité',
        'Juriste Social',
        'Responsable Relations Sociales',
        'Chargé de Relations Sociales',

        // Commercial / ADV / marketing
        'Responsable Commercial',
        'Chef de Vente',
        'Ingénieur Commercial',
        'Ingénieur Technico-Commercial',
        'Business Developer',
        'Key Account Manager',
        'Commercial Terrain',
        'Commercial Sédentaire',
        'Attaché Commercial',
        'Chargé d\'Affaires',
        'Chargé de Clientèle',
        'Responsable Grands Comptes',
        'Responsable ADV',
        'Assistant ADV',
        'Assistant Commercial',
        'Assistant Export',
        'Assistant Import-Export',
        'Responsable Marketing',
        'Chef de Produit',
        'Chargé de Marketing',
        'Chargé de Communication',
        'Community Manager',
        'Responsable E-commerce',
        'Responsable Service Après-Vente',
        'Conseiller Service Client',
        'Téléconseiller',
        'Chargé de Support Clients',

        // Informatique / ERP / data
        'Développeur ERP',
        'Consultant ERP',
        'Chef de Projet ERP',
        'Analyste Fonctionnel ERP',
        'Administrateur ERP',
        'Consultant SAP',
        'Consultant Oracle',
        'Consultant Microsoft Dynamics',
        'Analyste Programmeur',
        'Développeur Full Stack',
        'Développeur Back-End',
        'Développeur Front-End',
        'Développeur .NET',
        'Développeur C#',
        'Développeur Web',
        'Intégrateur Web',
        'Architecte Logiciel',
        'Chef de Projet IT',
        'Product Owner',
        'Scrum Master',
        'Administrateur Système',
        'Administrateur Réseau',
        'Administrateur Base de Données',
        'Ingénieur Systèmes et Réseaux',
        'Ingénieur DevOps',
        'Ingénieur Cloud',
        'Technicien Support',
        'Technicien Support Informatique',
        'Technicien Helpdesk',
        'Technicien Réseaux',
        'Technicien Systèmes',
        'Ingénieur Cybersécurité',
        'Analyste Cybersécurité',
        'Data Analyst',
        'Data Engineer',
        'Data Scientist',
        'Analyste BI',
        'Consultant BI',
        'Responsable BI',
        'UX Designer',
        'UI Designer',
        'Testeur Logiciel',
        'Ingénieur QA Logicielle',

        // Bureautique / administratif
        'Assistant Administratif',
        'Assistant Polyvalent',
        'Assistant de Direction',
        'Assistant de Gestion',
        'Secrétaire',
        'Secrétaire de Direction',
        'Secrétaire Commerciale',
        'Opérateur de Saisie',
        'Employé Administratif',
        'Agent Administratif',
        'Standardiste',
        'Réceptionniste',
        'Office Manager',
        'Assistant Projet',
        'Assistant Logistique',
        'Assistant Comptable',
        'Assistant Contrôle de Gestion',
        'Assistant Qualité',
        'Assistant HSE',

        // Bureau d\'études / projets / ingénierie
        'Technicien Bureau d\'Études',
        'Dessinateur-Projeteur',
        'Projeteur Mécanique',
        'Projeteur Électrique',
        'Ingénieur Études',
        'Chargé d\'Études',
        'Chef de Projet Industriel',
        'Chef de Projet Méthodes',
        'Chef de Projet Logistique',
        'Chef de Projet Supply Chain',
        'Chef de Projet Digital',
        'Chef de Projet Transformation',
        'Project Manager Officer (PMO)',
        'Coordinateur de Projet',

        // Autres fonctions support
        'Juriste d\'Entreprise',
        'Responsable Juridique',
        'Responsable RSE',
        'Chargé de Mission RSE',
        'Responsable Sécurité Site',
        'Responsable Services Généraux',
        'Acheteur Hors Production',
        'Acheteur Services',
        'Responsable Immobilier',
        'Gestionnaire Parc Automobile',

        // Entrée de gamme / jeunes diplômés
        'Stagiaire',
        'Stagiaire Ingénieur',
        'Stagiaire Comptabilité',
        'Stagiaire Informatique',
        'Stagiaire Logistique',
        'Stagiaire Qualité',
        'Alternant',
        'Apprenti',
        'Assistant Junior',
        'Analyste Junior'
    ];

    function selectJob(job) {
        input.value = job;
        hiddenSelected.value = job;
        closeDropdown();
    }

    function populateDropdownFromList(list) {
        dropdown.innerHTML = '';

        if (!list || list.length === 0) {
            const option = document.createElement('div');
            option.className = 'job-option job-option-muted';
            option.textContent = 'Aucun poste trouvé';
            dropdown.appendChild(option);
            return;
        }

        list.forEach(job => {
            const option = document.createElement('div');
            option.className = 'job-option';
            option.textContent = job;
            option.addEventListener('click', () => selectJob(job));
            dropdown.appendChild(option);
        });
    }

    function openDropdown() {
        if (!dropdown.classList.contains('visible')) {
            populateDropdownFromList(jobPositions);
        }
        dropdown.classList.add('visible');
        const icon = document.querySelector('.job-input-icon');
        if (icon) {
            icon.style.transform = 'translateY(-50%) rotate(180deg)';
        }
    }

    function closeDropdown() {
        dropdown.classList.remove('visible');
        const icon = document.querySelector('.job-input-icon');
        if (icon) {
            icon.style.transform = 'translateY(-50%) rotate(0deg)';
        }
    }

    function filterJobs(query) {
        const q = query.trim().toLowerCase();
        if (q === '') {
            populateDropdownFromList(jobPositions);
            return;
        }

        const filtered = jobPositions.filter(job =>
            job.toLowerCase().includes(q)
        );

        populateDropdownFromList(filtered);
    }

    // Événements
    input.addEventListener('focus', () => {
        openDropdown();
        filterJobs(input.value || '');
    });

    input.addEventListener('input', (e) => {
        const query = e.target.value;
        openDropdown();
        filterJobs(query);
    });

    input.addEventListener('blur', () => {
        setTimeout(closeDropdown, 200);
    });

    document.addEventListener('click', (e) => {
        if (!dropdown.contains(e.target) && e.target !== input) {
            closeDropdown();
        }
    });

    if (hiddenSelected.value) {
        input.value = hiddenSelected.value;
    }
})();
