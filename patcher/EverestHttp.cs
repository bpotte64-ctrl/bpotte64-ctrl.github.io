using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MonoMod;

[MonoModLinkFrom("Celeste.Mod.Helpers.CompressedHttpClient")]
public class RegularHttpClient : HttpClient
{
    public RegularHttpClient() : base()
    {
        // DefaultRequestHeaders.Add("User-Agent", $"Everest/{Everest.VersionString}");
        DefaultRequestHeaders.Add("User-Agent", $"Everest/1.0.0");
    }
}


namespace Celeste.Mod
{
    public static partial class patch_Everest
    {

        public static partial class patch_Updater
        {
            public static void DownloadFileWithProgress(string url, string destPath, Func<int, long, int, bool> progressCallback)
            {
                DateTime timeStart = DateTime.Now;

                if (File.Exists(destPath))
                    File.Delete(destPath);

                using (HttpClient client = new RegularHttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(10000);
                    client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

                    Task<HttpResponseMessage> responseTask = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    HttpResponseMessage response;
                    try
                    {
                        response = responseTask.Result;
                    }
                    catch (AggregateException ae)
                    {
                        // GetAsync throws a TaskCanceledException if the client times out instead of a TimeoutException
                        // ":screwms:" ~Popax21
                        if (responseTask.IsCanceled)
                            throw new TimeoutException($"The request to {url} timed out.", ae.InnerException);

                        // don't "throw ex;" here, as that resets the stacktrace (which is bad)
                        throw;
                    }

                    // Manual buffered copy from web input to file output.
                    // Allows us to measure speed and progress.
                    using (response)
                    using (Stream input = response.Content.ReadAsStreamAsync().Result)
                    using (FileStream output = File.OpenWrite(destPath))
                    {
                        if (input.CanTimeout)
                            input.ReadTimeout = 10000;

                        long length;
                        if (input.CanSeek)
                        {
                            length = input.Length;
                        }
                        else
                        {
                            length = response.Content.Headers.ContentLength ?? 0;
                        }

                        progressCallback(0, length, 0);

                        byte[] buffer = new byte[4096];
                        DateTime timeLastSpeed = timeStart;
                        int read = 1;
                        int readForSpeed = 0;
                        int pos = 0;
                        int speed = 0;
                        int count = 0;
                        TimeSpan td;
                        while (read > 0)
                        {
                            count = length > 0 ? (int)Math.Min(buffer.Length, length - pos) : buffer.Length;
                            read = input.Read(buffer, 0, count);
                            output.Write(buffer, 0, read);
                            pos += read;
                            readForSpeed += read;

                            td = DateTime.Now - timeLastSpeed;
                            if (td.TotalMilliseconds > 100)
                            {
                                speed = (int)((readForSpeed / 1024D) / td.TotalSeconds);
                                readForSpeed = 0;
                                timeLastSpeed = DateTime.Now;
                            }

                            if (!progressCallback(pos, length, speed))
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
