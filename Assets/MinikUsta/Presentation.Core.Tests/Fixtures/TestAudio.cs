#nullable enable
// The real sounds on a recording player for the tests (paket V2): every recipe of Resources/audio/sounds.json rendered up front
// (rendered once per test run), `RecordRequests` on, so a test reads what the game ASKED to play (name, pitch, gain) in order.
using MinikUsta.Presentation.Audio;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public static class TestAudio
    {
        private static SoundBank? shared;

        /// <summary>A player whose bank holds every sound (rendered once and shared); requests are recorded. No output (nothing to hear).</summary>
        public static AudioPlayer Player(bool needsUnlock = false)
        {
            if (shared == null)
            {
                var bank = new SoundBank(SoundSet.FromText(PresentationPaths.ResourceText("audio/sounds")));
                bank.RequestAll();
                while (bank.Pending > 0) bank.Pump(1 << 20);
                shared = bank; // read-only from here on: every test's player shares the rendered buffers
            }
            return new AudioPlayer(shared, null, needsUnlock) { RecordRequests = true };
        }
    }
}
