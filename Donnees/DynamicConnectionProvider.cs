// Fichier : Donnees/DynamicConnectionProvider.cs
namespace Donnees
{
    // Service très simple qui garde la connection string actuelle en mémoire
    public class DynamicConnectionProvider
    {
        // Exemple :
        // Host=localhost;Port=5432;Database=erp_client1;Username=openpg;Password=xxxx
        public string CurrentConnectionString { get; set; } = string.Empty;
    }
}
