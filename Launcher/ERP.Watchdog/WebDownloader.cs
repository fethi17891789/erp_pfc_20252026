using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ERP.Watchdog
{
    public static class WebDownloader
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<bool> DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string?> DownloadStringAsync(string url)
        {
            try
            {
                return await _httpClient.GetStringAsync(url);
            }
            catch
            {
                return null;
            }
        }
    }
}
