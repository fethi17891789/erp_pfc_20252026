using DnsClient;
using PhoneNumbers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Metier.CRM
{
    public class CompanyInfo
    {
        public string FullName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class ValidationService
    {
        public async Task<CompanyInfo> ExtractCompanyInfoAsync(string url)
        {
            var info = new CompanyInfo();
            if (string.IsNullOrWhiteSpace(url)) return info;

            url = url.Trim().ToLower();
            if (!url.StartsWith("http")) url = "https://" + url;

            Console.WriteLine($"[CRM] Tentative d'enrichissement pour : {url}");

            try
            {
                using var httpClient = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { 
                    AllowAutoRedirect = true,
                    CheckCertificateRevocationList = false,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // On ignore les erreurs SSL pour le scraping
                });

                httpClient.Timeout = TimeSpan.FromSeconds(8);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[CRM] Échec HTTP {response.StatusCode} pour {url}");
                    // Si HTTPS échoue, on peut tenter HTTP (rare mais existe)
                    if (url.StartsWith("https://"))
                    {
                         url = "http://" + url.Substring(8);
                         response = await httpClient.GetAsync(url);
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);

                    // 1. Extraction du Titre (Nom)
                    var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                    var ogTitleNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title' or @name='og:title']");
                    
                    if (ogTitleNode != null && !string.IsNullOrEmpty(ogTitleNode.GetAttributeValue("content", "")))
                    {
                        info.FullName = System.Net.WebUtility.HtmlDecode(ogTitleNode.GetAttributeValue("content", "")).Trim();
                    }
                    else if (titleNode != null)
                    {
                        info.FullName = System.Net.WebUtility.HtmlDecode(titleNode.InnerText).Trim();
                    }
                    
                    // Nettoyage du titre (enlever les suffixes " | Accueil" etc)
                    if (!string.IsNullOrEmpty(info.FullName))
                    {
                        var separators = new[] { " - ", " | ", " : ", " • " };
                        foreach (var sep in separators)
                        {
                            if (info.FullName.Contains(sep))
                                info.FullName = info.FullName.Split(sep)[0];
                        }
                    }

                    // 2. Extraction de la Description (Notes)
                    var descNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description' or @property='og:description']");
                    if (descNode != null && !string.IsNullOrEmpty(descNode.GetAttributeValue("content", "")))
                    {
                        info.Comment = System.Net.WebUtility.HtmlDecode(descNode.GetAttributeValue("content", "")).Trim();
                    }

                    Console.WriteLine($"[CRM] Succès : {info.FullName} extrait.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRM] Erreur Scraping {url} : {ex.Message}");
            }

            // Fallback : si on n'a rien trouvé du tout, on met au moins le nom de domaine
            if (string.IsNullOrEmpty(info.FullName))
            {
                var uri = new Uri(url.StartsWith("http") ? url : "https://" + url);
                info.FullName = uri.Host.Replace("www.", "");
                if (info.FullName.Contains(".")) info.FullName = char.ToUpper(info.FullName[0]) + info.FullName.Substring(1).Split('.')[0];
            }

            return info;
        }

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

                if (response.StartsWith("550")) return false;

                return true;
            }
            catch
            {
                return true; 
            }
        }
    }
}
