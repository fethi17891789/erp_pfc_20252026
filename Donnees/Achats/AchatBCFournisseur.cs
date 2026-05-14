namespace Donnees.Achats
{
    public class AchatBCFournisseur
    {
        public int Id { get; set; }
        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }
        public int FournisseurId { get; set; }
        public Contact? Fournisseur { get; set; }
    }
}
