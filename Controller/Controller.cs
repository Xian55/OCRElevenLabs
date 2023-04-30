using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NAudio.Wave;

using Protonox.Labs.Api;

using Serilog.Core;

using Tesseract;

namespace Protonox
{
    public sealed class Controller
    {
        private readonly ILogger logger;
        private readonly ElevenLabs labs;
        private readonly VoiceSettings voiceSettings;
        private readonly AudioSettings audioSettings;

        private readonly TesseractEngine ocr;

        public Controller(ILogger logger, ElevenLabs labs, VoiceSettings settings,
            AudioSettings audioSettings, TesseractEngine ocr)
        {
            this.logger = logger;
            this.labs = labs;
            this.voiceSettings = settings;

            this.audioSettings = audioSettings;

            this.ocr = ocr;
        }

        public static void Shutdown()
        {
            System.Windows.Application.Current.Shutdown();
        }

        public int GenAudioHash(string text) =>
            HashCode.Combine(audioSettings.Voice.DetHashGen(),
                text.DetHashGen(),
                voiceSettings.GetHashCode());

        public string AudioOutput(string text)
        {
            UpdateVoiceSettings();
            return Path.Combine(labs.Output,
                GenAudioHash(text).ToString() + ElevenLabs.EXT);
        }

        public string StartOCRThread(Rectangle rect)
        {
            string text = string.Empty;

            Thread thread = new(process);
            thread.Start();
            thread.Join();

            logger.LogInformation($"[OCR] {text}");

            return text;
            void process() => text = Extract(rect);
        }

        private string Extract(Rectangle rect)
        {
            using Bitmap bitmap = Screenshot.Get(rect.Location, rect);

            using var page = ocr.Process(bitmap);

            return page.GetText().Trim();
        }

        public void Playback(string filePath)
        {
            AudioFileReader reader = new(filePath)
            {
                Volume = audioSettings.Volume
            };

            WaveOut wavePlayer = new();
            wavePlayer.Init(reader);
            wavePlayer.Play();
        }

        public Task<Voices[]> GetInitVoices()
        {
            return labs.GetInitVoices();
        }

        public async Task<bool> PostTTS(string text)
        {
            UpdateVoiceSettings();

            Voices voice = labs.Voices[audioSettings.Voice];

            return await labs.PostTTS(voice.voice_id,
                text, voiceSettings, GenAudioHash(text));
        }

        public void UpdateVoiceSettings()
        {
            voiceSettings.similarity_boost = audioSettings.Similarity;
            voiceSettings.stability = audioSettings.Stability;
        }
    }
}
