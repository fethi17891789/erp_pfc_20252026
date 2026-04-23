using DnsClient;
using PhoneNumbers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;

namespace Metier.CRM
{
    public class CompanyInfo
    {
        public string FullName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }

    public class ValidationService
    {
        public async Task<CompanyInfo> ExtractCompanyInfoAsync(string url)
        {
            var info = new CompanyInfo();
            if (string.IsNullOrWhiteSpace(url)) return info;

            url = url.Trim().ToLower();
            if (!url.StartsWith("http")) url = "https://" + url;

            var allPhoneCandidates = new HashSet<string>();
            var visitedUrls = new HashSet<string>();

            Console.WriteLine($"[CRM TURBO] Lancement du scan radar pour : {url}");

            try
            {
                using var httpClient = new HttpClient(new HttpClientHandler {
                    AllowAutoRedirect = true
                });

                httpClient.Timeout = TimeSpan.FromSeconds(8);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                // --- ETAPE 1 : SCAN DE LA HOME (Prioritaire) ---
                var homeResponse = await httpClient.GetAsync(url);
                if (!homeResponse.IsSuccessStatusCode) return info;

                var homeHtml = await homeResponse.Content.ReadAsStringAsync();
                var homeDoc = new HtmlAgilityPack.HtmlDocument();
                homeDoc.LoadHtml(homeHtml);
                visitedUrls.Add(url);

                // Extraction de base sur la Home
                ExtractBasicInfo(homeDoc, info);
                ExtractContactData(homeHtml, info, allPhoneCandidates);

                // FAST-TRACK : Si on a déjà l'essentiel, on s'arrête là (vitesse maximale)
                if (!string.IsNullOrEmpty(info.Email) && allPhoneCandidates.Count >= 1) {
                    Console.WriteLine("[CRM TURBO] Données complètes sur la Home. Sortie précoce.");
                    FinalizePhoneData(info, allPhoneCandidates);
                    info.Website = url;
                    return info;
                }

                // --- ETAPE 2 : IDENTIFICATION DES LIENS CONTACTS ---
                var linksToScan = new List<string>();
                var aNodes = homeDoc.DocumentNode.SelectNodes("//a[@href]");
                if (aNodes != null)
                {
                    foreach (var link in aNodes)
                    {
                        var href = link.GetAttributeValue("href", "").ToLower();
                        var text = link.InnerText.ToLower();
                        bool isInteresting = (href.Contains("contact") || text.Contains("contact") || 
                                           href.Contains("a-propos") || text.Contains("about") ||
                                           href.Contains("legal") || href.Contains("mentions")) 
                                           && !href.Contains("facebook") && !href.Contains("linkedin") && !href.Contains("twitter");

                        if (isInteresting)
                        {
                            if (href.StartsWith("/")) {
                                href = new Uri(new Uri(url), href).ToString();
                            }
                            else if (!href.StartsWith("http")) continue;

                            if (!visitedUrls.Contains(href) && linksToScan.Count < 2) {
                                linksToScan.Add(href);
                                visitedUrls.Add(href);
                            }
                        }
                    }
                }

                // --- ETAPE 3 : SCAN PARALLÈLE (Multi-thread) ---
                if (linksToScan.Any())
                {
                    Console.WriteLine($"[CRM TURBO] Scan parallèle de {linksToScan.Count} pages secondaires...");
                    var tasks = linksToScan.Select(async link => {
                        try {
                            using var resp = await httpClient.GetAsync(link);
                            if (resp.IsSuccessStatusCode) {
                                var html = await resp.Content.ReadAsStringAsync();
                                ExtractContactData(html, info, allPhoneCandidates);
                            }
                        } catch { }
                    });
                    await Task.WhenAll(tasks);
                }

                FinalizePhoneData(info, allPhoneCandidates);
                info.Website = url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRM SPIDER] Erreur critique : {ex.Message}");
            }

            // Fallback Nom si vide
            if (string.IsNullOrEmpty(info.FullName))
            {
                var uri = new Uri(url.StartsWith("http") ? url : "https://" + url);
                info.FullName = uri.Host.Replace("www.", "");
                if (info.FullName.Contains(".")) info.FullName = char.ToUpper(info.FullName[0]) + info.FullName.Substring(1).Split('.')[0];
            }

            return info;
        }

        private void ExtractContactData(string html, CompanyInfo info, HashSet<string> allPhoneCandidates)
        {
            // Emails
            var emailMatch = Regex.Match(html, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            if (emailMatch.Success && string.IsNullOrEmpty(info.Email)) info.Email = emailMatch.Value;

            // Téléphones
            var phoneMatches = Regex.Matches(html, @"(\+?\d{1,3}[-.\s]?\(?\d{1,3}?\)?[-.\s]?\d{2,4}[-.\s]?\d{2,4}[-.\s]?\d{2,4})");
            foreach (Match m in phoneMatches)
            {
                var p = m.Value.Trim();
                if (p.Length >= 10 && p.Any(char.IsDigit) && !p.StartsWith("202")) {
                    allPhoneCandidates.Add(p);
                }
            }
        }

        private void FinalizePhoneData(CompanyInfo info, HashSet<string> allPhoneCandidates)
        {
            if (allPhoneCandidates.Any())
            {
                info.Phone = string.Join(" | ", allPhoneCandidates.Take(10));
            }
        }

        private void ExtractBasicInfo(HtmlAgilityPack.HtmlDocument doc, CompanyInfo info)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            var ogTitleNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title' or @name='og:title']");
            
            if (ogTitleNode != null && !string.IsNullOrEmpty(ogTitleNode.GetAttributeValue("content", "")))
            {
                info.FullName = WebUtility.HtmlDecode(ogTitleNode.GetAttributeValue("content", "")).Trim();
            }
            else if (titleNode != null)
            {
                info.FullName = WebUtility.HtmlDecode(titleNode.InnerText).Trim();
            }
            
            if (!string.IsNullOrEmpty(info.FullName))
            {
                var trash = new[] { "Accueil", "Home", "Page d'accueil", "Site officiel", "Bienvenue sur", "Welcome to" };
                foreach (var t in trash) info.FullName = info.FullName.Replace(t, "", StringComparison.OrdinalIgnoreCase).Trim();

                var separators = new[] { " - ", " | ", " : ", " • ", " – " };
                foreach (var sep in separators)
                {
                    if (info.FullName.Contains(sep))
                        info.FullName = info.FullName.Split(sep)[0];
                }
                info.FullName = info.FullName.Trim(" \t\n\r-|:•".ToCharArray());
            }

            var descNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description' or @property='og:description']");
            if (descNode != null && !string.IsNullOrEmpty(descNode.GetAttributeValue("content", "")))
            {
                info.Comment = WebUtility.HtmlDecode(descNode.GetAttributeValue("content", "")).Trim();
            }
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

        public async Task<bool> ValidateWebsiteAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            url = url.Trim();
            if (!url.StartsWith("http")) url = "https://" + url;

            try {
                using var handler = new HttpClientHandler {
                    AllowAutoRedirect = true,
                    CheckCertificateRevocationList = false,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // HEAD d'abord (plus léger), fallback GET si le serveur refuse HEAD
                try {
                    var headResp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    return (int)headResp.StatusCode < 500;
                } catch {
                    var getResp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    return (int)getResp.StatusCode < 500;
                }
            } catch {
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
                return result.Answers.MxRecords().Any();
            }
            catch
            {
                return true;
            }
        }
    }
}
