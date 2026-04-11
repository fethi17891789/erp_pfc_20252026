using DnsClient;
using PhoneNumbers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Metier.CRM
{
    public class ValidationService
    {
        public bool ValidatePhone(string phone, string countryCode = "FR")
        {
            try
            {
                var phoneNumberUtil = PhoneNumberUtil.GetInstance();
                var phoneNumber = phoneNumberUtil.Parse(phone, countryCode);
                return phoneNumberUtil.IsValidNumber(phoneNumber);
            }
            catch (NumberParseException)
            {
                return false;
            }
        }

        public async Task<bool> ValidateEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                return false;

            var parts = email.Split('@');
            if (parts.Length != 2) return false;

            var domain = parts[1];

            try
            {
                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(domain, QueryType.MX);
                
                var mxRecords = result.Answers.MxRecords().OrderBy(mx => mx.Preference).ToList();
                if (!mxRecords.Any())
                    return false; // Pas de serveur mail = email invalide

                var mxHost = mxRecords.First().Exchange.Value;
                return await EmailHandshake(mxHost, email);
            }
            catch
            {
                // En cas d'erreur de requête DNS, on le marque invalide
                return false;
            }
        }

        private async Task<bool> EmailHandshake(string mxHost, string email)
        {
            try
            {
                using var client = new TcpClient();
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;

                var connTask = client.ConnectAsync(mxHost, 25);
                // On met un timeout de 3 sec. Si bloqué par FAI, on renvoie true par défaut par sécurité.
                if (await Task.WhenAny(connTask, Task.Delay(3000)) != connTask) return true; 

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                string response = await reader.ReadLineAsync() ?? "";
                if (!response.StartsWith("220")) return true; 

                await writer.WriteLineAsync("HELO erp-pfc.local");
                response = await reader.ReadLineAsync() ?? "";
                if (!response.StartsWith("250")) return true;

                await writer.WriteLineAsync("MAIL FROM:<test@erp-pfc.local>");
                response = await reader.ReadLineAsync() ?? "";
                if (!response.StartsWith("250")) return true;

                await writer.WriteLineAsync($"RCPT TO:<{email}>");
                response = await reader.ReadLineAsync() ?? "";
                
                await writer.WriteLineAsync("QUIT");

                // Le serveur refuse explicitement l'adresse (Code 550) = la boite n'existe pas.
                if (response.StartsWith("550")) return false;

                return true;
            }
            catch
            {
                // Les pare-feux des FAI bloquent souvent le port 25. 
                // Dans le doute, si on ne peut pas pinguer, on autorise l'email.
                return true; 
            }
        }
    }
}
