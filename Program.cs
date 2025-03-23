using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A wrapper class for managing a GStreamer process.
/// </summary>
public class ProcessWrapper
{
    /// <summary>
    /// The GStreamer process instance being managed.
    /// </summary>
    public Process GStreamerProcess { get; }

    /// <summary>
    /// Initializes a new instance of the ProcessWrapper class.
    /// </summary>
    /// <param name="gstreamerProcess">The GStreamer process to wrap.</param>
    public ProcessWrapper(Process gstreamerProcess)
    {
        GStreamerProcess = gstreamerProcess;
    }

    /// <summary>
    /// Gets a value indicating whether the GStreamer process has exited.
    /// </summary>
    public bool IsExited
    {
        get
        {
            return GStreamerProcess == null || GStreamerProcess.HasExited;
        }
    }
}

/// <summary>
/// Main program class for the EnigmaToOculus backend.
/// Handles downloading IPTV channel lists, streaming channels using GStreamer, and serving HTTP requests.
/// </summary>
class Program
{
    /// <summary>
    /// HTTP client used for downloading the M3U playlists.
    /// </summary>
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Path to the output directory where .ts files will be saved.
    /// </summary>
    private static string _outputPath;

    /// <summary>
    /// The current GStreamer process wrapper for the active streaming session.
    /// </summary>
    private static ProcessWrapper _currentProcess;

    /// <summary>
    /// The file path of the current .ts output file being generated.
    /// </summary>
    private static string _currentOutputFile;

    /// <summary>
    /// The name of the channel currently being streamed.
    /// </summary>
    private static string _currentChannelName;

    /// <summary>
    /// The start time of the current streaming session, or null if no stream is active.
    /// </summary>
    private static DateTime? _startTime;

    /// <summary>
    /// The URL of the channel currently being streamed.
    /// </summary>
    private static string _currentUrl;

    /// <summary>
    /// List of all parsed channels from both M3U files (OpenWebif and open list).
    /// </summary>
    private static System.Collections.Generic.List<Channel> _allChannels;

    /// <summary>
    /// Entry point of the application.
    /// Initializes the environment, downloads the M3U playlists, and starts the HTTP server.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    static async Task Main(string[] args)
    {
        try
        {
            // Retrieve environment variables
            string m3uUrl = Environment.GetEnvironmentVariable("M3U_URL") ?? throw new Exception("M3U_URL no definido");
            string channelsEnv = Environment.GetEnvironmentVariable("CHANNELS") ?? ""; // Permitir que esté vacío
            _outputPath = Environment.GetEnvironmentVariable("OUTPUT_DIR") ?? throw new Exception("OUTPUT_DIR no definido");
            // Retrieve the open list URL from environment variable, with a default fallback
            string openListUrl = Environment.GetEnvironmentVariable("OPEN_M3U_URL") ?? "https://www.tdtchannels.com/lists/tv.m3u";
            string cachePathOpenWebif = Path.Combine("/cache", "channels_openwebif.m3u");
            string cachePathOpenList = Path.Combine("/cache", "channels_openlist.m3u");

            // Parse desired channels from the CHANNELS environment variable (not used since we don't want filtering)
            string[] desiredChannels = channelsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray();
            if (desiredChannels.Length > 0)
            {
                Console.WriteLine($"Canales deseados (ignorados): {string.Join(", ", desiredChannels)}");
            }
            else
            {
                Console.WriteLine("No se especificaron canales deseados. Se usarán todos los canales disponibles.");
            }

            // Ensure the cache directory exists
            Directory.CreateDirectory("/cache");
            Console.WriteLine("Directorio /cache creado o ya existe.");

            // Initialize the combined channels list
            _allChannels = new System.Collections.Generic.List<Channel>();

            // Download and parse channels from the OpenWebif M3U URL
            try
            {
                var openWebifChannels = await DownloadAndParseM3u(m3uUrl, cachePathOpenWebif, desiredChannels, false);
                Console.WriteLine($"Total de canales parseados desde OpenWebif: {openWebifChannels.Count}");
                _allChannels.AddRange(openWebifChannels);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar la lista de OpenWebif ({m3uUrl}): {ex.Message}");
            }

            // Download and parse channels from the open list M3U URL, marking them as (open)
            try
            {
                var openListChannels = await DownloadAndParseM3u(openListUrl, cachePathOpenList, desiredChannels, true);
                Console.WriteLine($"Total de canales parseados desde la lista abierta: {openListChannels.Count}");
                _allChannels.AddRange(openListChannels);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar la lista abierta ({openListUrl}): {ex.Message}");
                Console.WriteLine("Continuando con la lista de OpenWebif únicamente...");
            }

            Console.WriteLine($"Total de canales combinados: {_allChannels.Count}");

            // No aplicar filtrado (usar todos los canales)
            var filteredChannels = _allChannels;
            Console.WriteLine($"Total de canales (sin filtrar): {filteredChannels.Count}");

            // Log all parsed channels for debugging
            Console.WriteLine("Canales parseados:");
            foreach (var channel in _allChannels)
            {
                Console.WriteLine($"- {channel.TvgName}");
            }

            // Ensure the output directory exists
            Directory.CreateDirectory(_outputPath);
            Console.WriteLine($"Directorio de salida {_outputPath} creado o ya existe.");

            // Start the HTTP server to handle requests
            await StartHttpServer(filteredChannels);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en Main: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Starts an HTTP server to handle requests for starting/stopping streams and retrieving channel information.
    /// </summary>
    /// <param name="filteredChannels">List of channels to serve (in this case, all channels without filtering).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    static async Task StartHttpServer(System.Collections.Generic.List<Channel> filteredChannels)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://*:6677/");
        listener.Start();
        Console.WriteLine($"Servidor HTTP iniciado. Escuchando en http://*:6677/");

        while (true)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                // Add CORS headers to allow cross-origin requests
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                // Handle OPTIONS (preflight) requests
                if (request.HttpMethod == "OPTIONS")
                {
                    Console.WriteLine("[DEBUG] Solicitud OPTIONS recibida.");
                    response.StatusCode = 200;
                    response.Close();
                    continue;
                }

                // Process the request based on the endpoint
                if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/start")
                {
                    var channelName = request.QueryString["channel"];
                    if (string.IsNullOrEmpty(channelName))
                    {
                        await SendResponse(response, 400, "Parámetro 'channel' no proporcionado.");
                        continue;
                    }

                    // Find the channel in the full list of channels
                    var channel = _allChannels.FirstOrDefault(c => c.TvgName.Equals(channelName, StringComparison.OrdinalIgnoreCase));
                    if (channel == null)
                    {
                        await SendResponse(response, 404, $"Canal '{channelName}' no encontrado en la lista completa de canales.");
                        continue;
                    }

                    // Stop any existing stream and clean up
                    if (_currentProcess != null && !_currentProcess.IsExited)
                    {
                        StopStreaming(_currentProcess);
                        await DeleteOutputFiles();
                        _currentProcess = null;
                        _currentOutputFile = null;
                        _currentChannelName = null;
                        _currentUrl = null;
                    }

                    // Delete any existing .ts files in the output directory
                    await DeleteOutputFiles();

                    // Start streaming the requested channel
                    string outputFile = Path.Combine(_outputPath, $"{SanitizeFileName(channel.TvgName)}.ts");

                    var process = await StartStreaming(channel.Url, outputFile);
                    _currentProcess = process;
                    _currentOutputFile = outputFile;
                    _currentChannelName = channel.TvgName;
                    _currentUrl = channel.Url;
                    _startTime = DateTime.Now;

                    // Wait to ensure GStreamer has started writing the file
                    await Task.Delay(20000); // 20 seconds

                    // Verify the output file exists before responding
                    if (!File.Exists(outputFile))
                    {
                        StopStreaming(_currentProcess);
                        await DeleteOutputFiles();
                        _currentProcess = null;
                        _currentOutputFile = null;
                        _currentChannelName = null;
                        _currentUrl = null;
                        await SendResponse(response, 500, "Error al iniciar el streaming: el archivo de salida no se creó.");
                        continue;
                    }

                    // Log the file size for debugging
                    var fileInfo = new FileInfo(outputFile);
                    Console.WriteLine($"Archivo {outputFile} creado con tamaño: {fileInfo.Length} bytes");

                    // Respond with the physical path of the output file
                    await SendResponse(response, 200, outputFile);
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/stop")
                {
                    if (_currentProcess != null && !_currentProcess.IsExited)
                    {
                        StopStreaming(_currentProcess);
                        await DeleteOutputFiles();
                        _currentProcess = null;
                        _currentOutputFile = null;
                        _currentChannelName = null;
                        _currentUrl = null;
                        _startTime = null;
                        await SendResponse(response, 200, "Streaming detenido y archivos de salida eliminados.");
                    }
                    else
                    {
                        // If no stream is active, still delete any existing files
                        await DeleteOutputFiles();
                        await SendResponse(response, 200, "No hay streaming activo. Archivos de salida eliminados por si acaso.");
                    }
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/status")
                {
                    // Return the current streaming status
                    var status = new
                    {
                        isStreaming = _currentProcess != null && !_currentProcess.IsExited,
                        channelName = _currentChannelName,
                        startTime = _startTime?.ToString("o") // ISO 8601 format
                    };
                    var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                    Console.WriteLine($"[DEBUG] Solicitud recibida para /status. Respuesta: {statusJson}");
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes(statusJson);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/channels")
                {
                    // Return the list of all parsed channels
                    Console.WriteLine($"[DEBUG] Solicitud recibida para /channels. Total de canales: {_allChannels.Count}");
                    var channelsJson = System.Text.Json.JsonSerializer.Serialize(_allChannels.Select(c => new
                    {
                        tvgName = c.TvgName,
                        tvgLogo = c.TvgLogo
                    }));
                    Console.WriteLine($"[DEBUG] Respuesta JSON: {channelsJson}");
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes(channelsJson);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                else
                {
                    await SendResponse(response, 404, "Ruta no encontrada. Usa /start?channel=<nombre_del_canal>, /stop o /channels");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar la solicitud: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sends an HTTP response with the specified status code and message.
    /// </summary>
    /// <param name="response">The HTTP response object.</param>
    /// <param name="statusCode">The HTTP status code to send.</param>
    /// <param name="message">The message to include in the response body.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    static async Task SendResponse(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        var buffer = Encoding.UTF8.GetBytes(message);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    /// <summary>
    /// Deletes all .ts files in the output directory.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    static async Task DeleteOutputFiles()
    {
        try
        {
            if (Directory.Exists(_outputPath))
            {
                var files = Directory.GetFiles(_outputPath, "*.ts");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Console.WriteLine($"Archivo eliminado: {file}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al eliminar el archivo {file}: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"El directorio {_outputPath} no existe.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al eliminar archivos de salida: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Downloads and parses an M3U playlist from the specified URL.
    /// </summary>
    /// <param name="m3uUrl">The URL of the M3U playlist to download.</param>
    /// <param name="cachePath">The file path to cache the M3U content.</param>
    /// <param name="desiredChannels">Array of desired channel names (not used in this method).</param>
    /// <param name="isOpenList">Indicates if the list is the open list, to append "(open)" to channel names.</param>
    /// <returns>A task that resolves to a list of parsed Channel objects.</returns>
    static async Task<System.Collections.Generic.List<Channel>> DownloadAndParseM3u(string m3uUrl, string cachePath, string[] desiredChannels, bool isOpenList)
    {
        var channels = new System.Collections.Generic.List<Channel>();
        string m3uContent;

        try
        {
            Console.WriteLine($"Descargando lista M3U desde {m3uUrl}...");
            var response = await _httpClient.GetAsync(m3uUrl);
            response.EnsureSuccessStatusCode(); // Lanza una excepción si la solicitud falla
            m3uContent = await response.Content.ReadAsStringAsync();
            File.WriteAllText(cachePath, m3uContent);
            Console.WriteLine("Lista M3U descargada y cacheada.");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error HTTP al descargar la lista M3U: {ex.Message}");
            Console.WriteLine($"Status Code: {ex.StatusCode}");
            if (File.Exists(cachePath))
            {
                Console.WriteLine("Usando lista cacheada...");
                m3uContent = File.ReadAllText(cachePath);
            }
            else
            {
                throw new Exception("No se pudo descargar la lista M3U y no hay caché disponible.", ex);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error general al descargar la lista M3U: {ex.Message}");
            if (File.Exists(cachePath))
            {
                Console.WriteLine("Usando lista cacheada...");
                m3uContent = File.ReadAllText(cachePath);
            }
            else
            {
                throw new Exception("No se pudo descargar la lista M3U y no hay caché disponible.", ex);
            }
        }

        var lines = m3uContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("#EXTINF"))
            {
                var tvgNameMatch = Regex.Match(lines[i], @"tvg-name=""([^""]+)""");
                var tvgIdMatch = Regex.Match(lines[i], @"tvg-id=""([^""]+)""");
                var tvgChnoMatch = Regex.Match(lines[i], @"tvg-chno=""([^""]+)""");
                var tvgLogoMatch = Regex.Match(lines[i], @"tvg-logo=""([^""]+)""");

                if (tvgNameMatch.Success && i + 1 < lines.Length)
                {
                    string tvgName = tvgNameMatch.Groups[1].Value.Trim();
                    // Append "(open)" to the channel name if it's from the open list
                    if (isOpenList)
                    {
                        tvgName = $"{tvgName} (open)";
                    }
                    string tvgId = tvgIdMatch.Success ? tvgIdMatch.Groups[1].Value.Trim() : string.Empty;
                    string tvgChno = tvgChnoMatch.Success ? tvgChnoMatch.Groups[1].Value.Trim() : string.Empty;
                    string tvgLogo = tvgLogoMatch.Success ? tvgLogoMatch.Groups[1].Value.Trim() : string.Empty;

                    // Check for #EXTVLCOPT (used in OpenWebif format)
                    if (i + 2 < lines.Length && lines[i + 1].StartsWith("#EXTVLCOPT"))
                    {
                        string url = lines[i + 2].Trim();

                        if (!string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                        {
                            var channel = new Channel
                            {
                                TvgName = tvgName,
                                TvgId = tvgId,
                                TvgChno = tvgChno,
                                TvgLogo = tvgLogo,
                                Url = url
                            };
                            channels.Add(channel);
                            Console.WriteLine($"[DEBUG] Canal parseado: {tvgName}, Logo: {tvgLogo}, URL: {url}");
                        }
                        else
                        {
                            Console.WriteLine($"URL inválida para el canal '{tvgName}': {url}");
                        }

                        i += 2;
                    }
                    // Standard M3U format (used by the open list)
                    else
                    {
                        string url = lines[i + 1].Trim();

                        if (!string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                        {
                            var channel = new Channel
                            {
                                TvgName = tvgName,
                                TvgId = tvgId,
                                TvgChno = tvgChno,
                                TvgLogo = tvgLogo,
                                Url = url
                            };
                            channels.Add(channel);
                            Console.WriteLine($"[DEBUG] Canal parseado: {tvgName}, Logo: {tvgLogo}, URL: {url}");
                        }
                        else
                        {
                            Console.WriteLine($"URL inválida para el canal '{tvgName}': {url}");
                        }

                        i += 1;
                    }
                }
                else
                {
                    Console.WriteLine("No se pudo parsear el nombre del canal (tvg-name no encontrado).");
                }
            }
        }

        Console.WriteLine($"Total de canales parseados: {channels.Count}");
        return channels;
    }

    /// <summary>
    /// Starts streaming a channel using GStreamer and saves the output to a .ts file.
    /// </summary>
    /// <param name="url">The URL of the channel to stream.</param>
    /// <param name="outputFile">The file path where the .ts file will be saved.</param>
    /// <param name="useGStreamer">Flag indicating whether to use GStreamer (default: true).</param>
    /// <returns>A task that resolves to a ProcessWrapper for the GStreamer process.</returns>
    static async Task<ProcessWrapper> StartStreaming(string url, string outputFile, bool useGStreamer = true)
    {
        outputFile = Path.ChangeExtension(outputFile, ".ts");

        var gstreamerProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gst-launch-1.0",
                Arguments = $"souphttpsrc location=\"{url}\" ! decodebin name=dec ! queue ! videoconvert ! x264enc ! mpegtsmux name=mux ! filesink location=\"{outputFile}\" dec. ! queue ! audioconvert ! avenc_aac ! mux.",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        string gstreamerStdOutput = "";
        string gstreamerErrorOutput = "";

        try
        {
            Console.WriteLine($"[DEBUG] Iniciando GStreamer con comando: gst-launch-1.0 {gstreamerProcess.StartInfo.Arguments}");
            bool gstreamerStarted = gstreamerProcess.Start();
            if (!gstreamerStarted)
            {
                Console.WriteLine("[ERROR] No se pudo iniciar GStreamer.");
                throw new InvalidOperationException("No se pudo iniciar GStreamer.");
            }

            Console.WriteLine($"[{DateTime.Now}] Proceso de streaming iniciado con PID: {gstreamerProcess.Id}");

            gstreamerProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    gstreamerStdOutput += e.Data + "\n";
                    Console.WriteLine($"[DEBUG] GStreamer stdout (StartStreaming): {e.Data}");
                }
            };
            gstreamerProcess.BeginOutputReadLine();

            gstreamerProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    gstreamerErrorOutput += e.Data + "\n";
                    Console.WriteLine($"[DEBUG] GStreamer stderr (StartStreaming): {e.Data}");
                }
            };
            gstreamerProcess.BeginErrorReadLine();

            await Task.Delay(15000); // Wait 15 seconds to allow GStreamer to start

            if (!File.Exists(outputFile))
            {
                Console.WriteLine($"[ERROR] El archivo de salida {outputFile} no se creó después de iniciar GStreamer.");
                gstreamerProcess.Kill();
                throw new InvalidOperationException("El archivo de salida no se creó.");
            }

            var fileInfo = new FileInfo(outputFile);
            Console.WriteLine($"Archivo {outputFile} creado con tamaño: {fileInfo.Length} bytes");

            return new ProcessWrapper(gstreamerProcess);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error al iniciar el proceso de streaming con GStreamer: {ex.Message}");
            Console.WriteLine($"[DEBUG] Salida estándar de GStreamer: {gstreamerStdOutput}");
            Console.WriteLine($"[DEBUG] Salida de error de GStreamer: {gstreamerErrorOutput}");
            if (gstreamerProcess != null && !gstreamerProcess.HasExited)
            {
                gstreamerProcess.Kill();
            }
            throw;
        }
    }

    /// <summary>
    /// Stops the streaming process managed by the specified ProcessWrapper.
    /// </summary>
    /// <param name="wrapper">The ProcessWrapper containing the GStreamer process to stop.</param>
    static void StopStreaming(ProcessWrapper wrapper)
    {
        if (wrapper != null && !wrapper.IsExited)
        {
            if (wrapper.GStreamerProcess != null && !wrapper.GStreamerProcess.HasExited)
            {
                try
                {
                    wrapper.GStreamerProcess.Kill();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error al detener GStreamer: {ex.Message}");
                }

                wrapper.GStreamerProcess.Close();
                wrapper.GStreamerProcess.Dispose();
            }
        }
    }

    /// <summary>
    /// Sanitizes a file name by replacing invalid characters with underscores.
    /// </summary>
    /// <param name="name">The file name to sanitize.</param>
    /// <returns>The sanitized file name.</returns>
    static string SanitizeFileName(string name)
    {
        return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}

/// <summary>
/// Represents a channel parsed from an M3U playlist.
/// </summary>
public class Channel
{
    /// <summary>
    /// The name of the channel (tvg-name).
    /// </summary>
    public string TvgName { get; set; }

    /// <summary>
    /// The ID of the channel (tvg-id).
    /// </summary>
    public string TvgId { get; set; }

    /// <summary>
    /// The channel number (tvg-chno).
    /// </summary>
    public string TvgChno { get; set; }

    /// <summary>
    /// The URL of the channel's logo (tvg-logo).
    /// </summary>
    public string TvgLogo { get; set; }

    /// <summary>
    /// The streaming URL of the channel.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Returns a string representation of the channel.
    /// </summary>
    /// <returns>A string containing the channel's details.</returns>
    public override string ToString()
    {
        return $"Channel: {TvgName}, TvgId: {TvgId}, TvgChno: {TvgChno}, Url: {Url}";
    }
}

/// <summary>
/// Extension methods for the Process class.
/// </summary>
static class ProcessExtensions
{
    /// <summary>
    /// Asynchronously waits for a process to exit.
    /// </summary>
    /// <param name="process">The process to wait for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the wait operation.</param>
    /// <returns>A task that completes when the process exits.</returns>
    public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) => tcs.TrySetResult(true);
        if (cancellationToken != default)
            cancellationToken.Register(() => tcs.TrySetCanceled());
        return tcs.Task;
    }
}