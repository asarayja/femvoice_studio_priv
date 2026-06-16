using System.Runtime.CompilerServices;

// FemVoice.Core exposes a few `internal` test hooks (e.g. ResonanceProxyEngine.EmitFormantsForTesting).
// The original single-project layout granted this to "FemVoiceStudio.Tests" from AudioCaptureService.cs.
// After the Linux portable-core split these types live in FemVoice.Core, so the grant must come from here.
// Test-visibility only — no behaviour change.
[assembly: InternalsVisibleTo("FemVoice.Tests.Portable")]
[assembly: InternalsVisibleTo("FemVoiceStudio.Tests")]
