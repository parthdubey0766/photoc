namespace PhotoC.Models;

/// <summary>
/// All user-configurable settings for PhotoC.
/// Loaded from and saved to %APPDATA%\PhotoC\appsettings.json.
/// </summary>
public class AppSettings
{
    /// <summary>Absolute path to the folder being watched.</summary>
    public string WatchedFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// JPEG re-encode quality (1–100). Default 80 — visually lossless.
    /// ShortPixel "Glossy" mode equivalent. At Q80, typical 10–20 MB phone photos
    /// compress to 1–3 MB with no perceptible quality loss (SSIM > 0.98).
    /// </summary>
    public int JPEGQuality { get; set; } = 80;

    /// <summary>
    /// Minimum JPEG quality used by adaptive smart compression trials.
    /// The sweep tests Q <see cref="JPEGQuality"/> down to this floor in steps of 3.
    /// Default 70 — matches ShortPixel Lossy mode floor. Still visually lossless
    /// for phone photos (SSIM > 0.96).
    /// </summary>
    public int MinJpegQuality { get; set; } = 70;

    /// <summary>
    /// Enables adaptive JPEG optimization by testing multiple qualities and keeping
    /// the smallest acceptable output (ShortPixel SmartCompress™-like strategy).
    /// </summary>
    public bool SmartAdaptiveCompression { get; set; } = true;

    /// <summary>PNG lossless compression level (0–9). Default 9 — maximum lossless compression.</summary>
    public int PNGCompressionLevel { get; set; } = 9;

    /// <summary>WebP encode quality (1–100). Default 85 — visually lossless.</summary>
    public int WebPQuality { get; set; } = 85;

    /// <summary>
    /// Maximum image dimension (longest edge, in pixels). Images larger than this
    /// are downscaled proportionally before compression. Default 0 (disabled — no
    /// resolution reduction). Set to 3840 for 4K downscaling if desired.
    /// Example: 48 MP (8000×6000) → 3840×2880 (~8.8 MP).
    /// </summary>
    public int MaxImageDimension { get; set; } = 0;

    /// <summary>
    /// Files smaller than this threshold (KB) are skipped — no compression needed.
    /// Default 2048 KB (2 MB).
    /// </summary>
    public int MinFileSizeKB { get; set; } = 2048;

    /// <summary>
    /// Files larger than this threshold (KB) are "large" targets — 60–95% reduction expected.
    /// Default 10240 KB (10 MB). Only used for logging/reporting.
    /// </summary>
    public int LargeFileSizeKB { get; set; } = 10240;

    /// <summary>File extensions to monitor (lower-case, with dot).</summary>
    public List<string> FileExtensions { get; set; } =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif"];

    /// <summary>Milliseconds to wait after last file event before processing. Default 5000.</summary>
    public int DebounceMilliseconds { get; set; } = 5000;

    /// <summary>How many times to retry a locked file before skipping. Default 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Minimum percentage size reduction required before replacing the original file.
    /// Prevents rewrite loops on near-identical outputs. Default 1.0%.
    /// </summary>
    public double MinSavingsPercent { get; set; } = 1.0;

    /// <summary>Whether to add a registry entry to run PhotoC on Windows startup.</summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>Directory where log files are written. Defaults to %APPDATA%\PhotoC\logs.</summary>
    public string LogPath { get; set; } = string.Empty;

    /// <summary>
    /// When true, compressed files are saved to <see cref="OutputFolderPath"/>
    /// instead of replacing the originals in-place.
    /// </summary>
    public bool SaveToOutputFolder { get; set; } = false;

    /// <summary>
    /// Absolute path to the folder where compressed copies are saved.
    /// Only used when <see cref="SaveToOutputFolder"/> is true.
    /// </summary>
    public string OutputFolderPath { get; set; } = string.Empty;
}
