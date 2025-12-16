namespace Donnees
{
    public class BDDEntity
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string DatabaseName { get; set; } = "erp_db";
        public string UserName { get; set; } = "openpg";
        public string Password { get; set; } = "fethi1234";

        public string MasterPassword { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
