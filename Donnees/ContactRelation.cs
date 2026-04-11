using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    public class ContactRelation
    {
        public int Id { get; set; }

        public int SourceContactId { get; set; }
        [ForeignKey("SourceContactId")]
        public virtual Contact? SourceContact { get; set; }

        public int TargetContactId { get; set; }
        [ForeignKey("TargetContactId")]
        public virtual Contact? TargetContact { get; set; }

        [Required, MaxLength(100)]
        public string RelationType { get; set; } = string.Empty; // ex: "Actionnaire de", "Employé chez"
    }
}
