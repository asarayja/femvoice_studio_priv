using System;
using System.IO;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Issues and persists the opaque per-install <b>participant token</b> used by Research
    /// Mode (Sprint E, Agent 5).
    ///
    /// <para><b>WHY THIS EXISTS.</b> Research exports must carry a stable participant key so
    /// a longitudinal series can be followed over time, WITHOUT carrying any personally
    /// identifiable value. The integer UserId is PII (it indexes the local user/DB) and must
    /// NEVER leave the device in a research dataset. This provider mints a random UUID on
    /// first run and reuses it forever after, giving research a stable-but-anonymous key.</para>
    ///
    /// <para><b>STORAGE.</b> The token lives in a small local JSON file under
    /// <c>LocalApplicationData/FemVoiceStudio/Research/participant-token.json</c> — the same
    /// file-store mechanism as <see cref="LocalVoiceGoalProfileStore"/>. It is deliberately
    /// kept OUT of the femvoice.db so it can never be joined back to the integer UserId.</para>
    ///
    /// <para><b>TESTABILITY.</b> The storage directory and the UUID factory are both
    /// injectable, so tests are fully deterministic and never depend on wall-clock time or
    /// real per-machine randomness in their assertions.</para>
    /// </summary>
    public sealed class ParticipantTokenProvider
    {
        private readonly string _directory;
        private readonly Func<string> _tokenFactory;
        private readonly object _sync = new();

        /// <summary>The on-disk file name holding the persisted token document.</summary>
        private const string FileName = "participant-token.json";

        /// <summary>
        /// Creates a provider.
        /// </summary>
        /// <param name="directory">
        /// Storage directory. When null, defaults to
        /// <c>LocalApplicationData/FemVoiceStudio/Research</c> (the LocalVoiceGoalProfileStore
        /// mechanism). Tests pass a temp directory for isolation.
        /// </param>
        /// <param name="tokenFactory">
        /// Factory that mints a brand-new token string when none is persisted yet. Defaults
        /// to a random UUID (<c>Guid.NewGuid().ToString("D")</c>). Tests inject a fixed value
        /// so assertions are deterministic and never time- or randomness-dependent.
        /// </param>
        public ParticipantTokenProvider(string? directory = null, Func<string>? tokenFactory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FemVoiceStudio",
                "Research");
            _tokenFactory = tokenFactory ?? (() => Guid.NewGuid().ToString("D"));
        }

        /// <summary>
        /// Returns the persisted participant token, minting and persisting a new one on the
        /// first call (first app start). Idempotent thereafter: every subsequent call — and
        /// every fresh provider instance over the same directory — returns the SAME token.
        ///
        /// <para>A corrupt or empty token file is treated as "no token yet" and a new token
        /// is minted and written, so a damaged file can never crash the app or leak a partial
        /// value.</para>
        /// </summary>
        public string GetOrCreateToken()
        {
            lock (_sync)
            {
                var existing = TryReadToken();
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing!;

                var token = _tokenFactory();
                Persist(token);
                return token;
            }
        }

        private string? TryReadToken()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return null;

            try
            {
                var document = JsonSerializer.Deserialize<TokenDocument>(
                    File.ReadAllText(path, System.Text.Encoding.UTF8));
                return document?.ParticipantToken;
            }
            catch
            {
                // Corrupt file ⇒ treat as "no token yet"; caller re-mints.
                return null;
            }
        }

        private void Persist(string token)
        {
            Directory.CreateDirectory(_directory);
            var document = new TokenDocument { ParticipantToken = token, CreatedAt = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetPath(), json, System.Text.Encoding.UTF8);
        }

        private string GetPath() => Path.Combine(_directory, FileName);

        /// <summary>
        /// On-disk JSON shape. <see cref="CreatedAt"/> is metadata only and is never used in
        /// any research output (it would itself be a weak time fingerprint).
        /// </summary>
        private sealed class TokenDocument
        {
            public string ParticipantToken { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}
