using System;
using System.IO;
using System.Media;
using System.Text;

namespace XWhip
{
    internal sealed class WhipSound : IDisposable
    {
        private readonly MemoryStream stream;
        private readonly SoundPlayer player;

        public WhipSound()
        {
            stream = new MemoryStream(BuildWave(), false);
            player = new SoundPlayer(stream);
            player.Load();
        }

        public void Play() { player.Play(); }
        public void Dispose() { player.Dispose(); stream.Dispose(); }

        internal static byte[] BuildWave()
        {
            const int rate = 22050;
            const double duration = .21;
            int count = (int)(rate * duration);
            var output = new MemoryStream(44 + count * 2);
            using (var writer = new BinaryWriter(output))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM.
                writer.Write((short)1); // Mono.
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(count * 2);
                var random = new Random(1731);
                double filtered = 0, previousNoise = 0;
                for (int i = 0; i < count; i++)
                {
                    double time = (double)i / rate;
                    double noise = random.NextDouble() * 2 - 1;
                    filtered = filtered * .65 + noise * .35;
                    double whoosh = time < .13 ?
                        .20 * filtered * Math.Sin(Math.PI * time / .13) * time / .13 : 0;
                    double snap = time >= .13 ?
                        .28 * (noise - previousNoise * .4) * Math.Exp(-(time - .13) * 95) : 0;
                    double fade = Math.Min(1, (duration - time) / .015);
                    writer.Write((short)Math.Round((whoosh + snap) * fade * 32767));
                    previousNoise = noise;
                }
            }
            return output.ToArray();
        }
    }
}
