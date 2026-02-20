// Fichier : Donnees/MRPConfigModule.cs
using System;

namespace Donnees
{
    public class MRPConfigModule
    {
        public int IdConfig { get; set; }

        public int HorizonParDefautJours { get; set; }

        public DateTime DateCreation { get; set; }

        public DateTime DateDerniereModification { get; set; }

        public int? CreeParUserId { get; set; }

        public int? ModifieParUserId { get; set; }
    }
}
