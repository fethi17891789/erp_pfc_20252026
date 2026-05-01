using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Donnees
{
    [Flags]
    public enum ContactRole
    {
        None = 0,
        Client = 1 << 0,
        Fournisseur = 1 << 1,
        Employe = 1 << 2,
        Partenaire = 1 << 3,
        Investisseur = 1 << 4
    }

    public class Contact
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Website { get; set; }

        public ContactRole Roles { get; set; } = ContactRole.None;

        public string? AvatarImage { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? AdresseComplete { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}
