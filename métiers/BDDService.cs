using Donnees;
using Npgsql;
using System.Threading.Tasks;

namespace Metier
{
    public class BDDService
    {
        public async Task<bool> CreateDatabaseIfNotExistsAsync(BDDEntity config)
        {
            // Connexion au serveur PostgreSQL (base postgres)
            var masterConnectionString =
                $"Host={config.Host};Port={config.Port};Username={config.UserName};Password={config.Password};Database=postgres";

            await using var connection = new NpgsqlConnection(masterConnectionString);
            await connection.OpenAsync();

            // Vérifier si la base existe déjà
            const string checkSql = "SELECT 1 FROM pg_database WHERE datname = @name;";
            await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
            {
                checkCmd.Parameters.AddWithValue("name", config.DatabaseName);
                var exists = await checkCmd.ExecuteScalarAsync();
                if (exists != null)
                {
                    // La base existe déjà
                    return false;
                }
            }

            // Créer la base
            var createSql = $"CREATE DATABASE \"{config.DatabaseName}\"";
            await using (var createCmd = new NpgsqlCommand(createSql, connection))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            return true;
        }

        // Optionnel : méthode de test
        public async Task TestCreateDatabaseAsync()
        {
            var config = new BDDEntity
            {
                Host = "localhost",
                Port = 5432,
                DatabaseName = "pfc_test1",
                UserName = "openpg",
                Password = "fethi1234"
            };

            var ok = await CreateDatabaseIfNotExistsAsync(config);

            if (ok)
                Console.WriteLine("TEST BDD: Database 'pfc_test1' created or already exists.");
            else
                Console.WriteLine("TEST BDD: Failed to create database 'pfc_test1'.");
        }
    }
}
