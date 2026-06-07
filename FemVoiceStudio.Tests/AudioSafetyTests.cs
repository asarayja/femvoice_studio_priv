using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Safety-tester for lydsikkerhet: ved tap av lydkilde skal analysen STOPPE og
    /// økten pauses — aldri stille fortsette på en ukjent kilde. Disse testene dekker
    /// det som er verifiserbart uten en fysisk lydenhet, via den interne testbare
    /// metoden HandleRecordingStopped (som OnRecordingStopped delegerer til).
    /// </summary>
    public class AudioSafetyTests
    {
        [Fact]
        public void HandleRecordingStopped_WithException_FiresDeviceLostWithReason()
        {
            using var service = new AudioCaptureService();

            string? lostReason = null;
            service.DeviceLost += (_, reason) => lostReason = reason;

            service.HandleRecordingStopped(new InvalidOperationException("Mikrofon frakoblet"));

            Assert.Equal("Mikrofon frakoblet", lostReason);
        }

        [Fact]
        public void HandleRecordingStopped_WithException_AlsoFiresErrorOccurred()
        {
            // Eksisterende oppførsel (ErrorOccurred) skal bevares i tillegg til DeviceLost.
            using var service = new AudioCaptureService();

            string? errorMessage = null;
            service.ErrorOccurred += (_, message) => errorMessage = message;

            service.HandleRecordingStopped(new InvalidOperationException("Driverfeil"));

            Assert.NotNull(errorMessage);
            Assert.Contains("Driverfeil", errorMessage);
        }

        [Fact]
        public void HandleRecordingStopped_WithoutException_DoesNotFireDeviceLost()
        {
            // Normal stopp (ingen exception) er ikke et enhetstap og skal ikke pause økten.
            using var service = new AudioCaptureService();

            var deviceLostFired = false;
            service.DeviceLost += (_, _) => deviceLostFired = true;

            service.HandleRecordingStopped(null);

            Assert.False(deviceLostFired);
        }

        [Fact]
        public void ActiveDeviceName_DefaultsToNull_BeforeRecordingStarts()
        {
            using var service = new AudioCaptureService();

            Assert.Null(service.ActiveDeviceName);
        }

        [Fact]
        public void DeviceLost_Unsubscribe_DoesNotThrowAndStopsReceiving()
        {
            using var service = new AudioCaptureService();

            var callCount = 0;
            EventHandler<string> handler = (_, _) => callCount++;

            service.DeviceLost += handler;
            service.DeviceLost -= handler;

            // Avmelding skal ikke kaste, og handleren skal ikke lenger motta hendelser.
            var ex = Record.Exception(() =>
                service.HandleRecordingStopped(new InvalidOperationException("frakoblet")));

            Assert.Null(ex);
            Assert.Equal(0, callCount);
        }

        [Fact]
        public void DeviceLost_WithNoSubscribers_DoesNotThrow()
        {
            // Safety-stien skal være robust selv uten abonnenter.
            using var service = new AudioCaptureService();

            var ex = Record.Exception(() =>
                service.HandleRecordingStopped(new InvalidOperationException("frakoblet")));

            Assert.Null(ex);
        }
    }
}
