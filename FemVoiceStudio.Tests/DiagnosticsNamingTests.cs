using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class DiagnosticsNamingTests
    {
        [Fact]
        public void DiagnosticsNaming_UsesProductNeutralNames_WithRc0ValidationProfile()
        {
            Assert.Equal("RC0", DiagnosticsNaming.ValidationProfile);
            Assert.Equal("EVIDENCE.json", DiagnosticsNaming.EvidenceJson);
            Assert.Equal("RUNTIME_LOG.txt", DiagnosticsNaming.RuntimeLog);
            Assert.Equal("VERIFICATION_REPORT.md", DiagnosticsNaming.VerificationReport);
            Assert.Contains("Diagnostics", DiagnosticsNaming.PrimaryRoot);
            Assert.Contains("RuntimeDiagnostics", DiagnosticsNaming.RuntimeDirectory);
        }

        [Fact]
        public void DiagnosticsNaming_KeepsRc0CompatibilityAliases()
        {
            Assert.True(DiagnosticsNaming.EnableRc0CompatibilityExport);
            Assert.Equal("RC0_EVIDENCE.json", DiagnosticsNaming.Rc0EvidenceJson);
            Assert.Equal("RC0_RUNTIME_LOG.txt", DiagnosticsNaming.Rc0RuntimeLog);
            Assert.Contains("RC0_Evidence", DiagnosticsNaming.LegacyPrimaryRoot);
        }

        [Theory]
        [InlineData("PASS", "PASS")]
        [InlineData("FAIL", "FAIL")]
        [InlineData("NOT_VERIFIED", "NOT_VERIFIED")]
        [InlineData("NOT_IMPLEMENTED", "NOT_IMPLEMENTED")]
        [InlineData("NOT_APPLICABLE", "NOT_VERIFIED")]
        [InlineData("", "NOT_VERIFIED")]
        public void ReportVerificationStatus_NormalizesAllowedValues(string input, string expected)
        {
            Assert.Equal(expected, Rc0EvidenceExporter.NormalizeVerificationStatus(input));
        }
    }
}
