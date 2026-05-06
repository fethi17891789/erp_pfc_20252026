using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ERP.Bootstrapper
{
    public static class WebDownloader
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<bool> DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                Console.WriteLine($"[DOWNLOAD] Démarrage du téléchargement : {Path.GetFileName(destinationPath)}");

                long totalBytes;
                long totalRead;
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canShowProgress = totalBytes != -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        totalRead = 0L;
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;

                                if (canShowProgress)
                                {
                                    var progress = (double)totalRead / totalBytes * 100;
                                    Console.Write($"\r[DOWNLOAD] Progression : {progress:F1}% ({totalRead / 1024 / 1024} Mo / {totalBytes / 1024 / 1024} Mo)");
                                }
                            }
                        } while (isMoreToRead);
                    }
                }

                if (totalBytes > 0 && totalRead != totalBytes)
                {
                    try { File.Delete(destinationPath); } catch { }
                    Console.WriteLine($"\n[ERREUR DOWNLOAD] Taille incomplète : {totalRead} / {totalBytes} octets. Fichier supprimé.");
                    return false;
                }

                Console.WriteLine("\n[DOWNLOAD] Succès : Téléchargement terminé.");
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
                Console.WriteLine($"\n[ERREUR DOWNLOAD] {ex.Message}");
                return false;
            }
        }
    }
}
