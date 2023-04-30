using System;

namespace Protonox.Labs.Api
{
    public sealed class VoicesArray
    {
        public Voices[] voices { get; set; }
    }

    public sealed class Voices
    {
        public string voice_id { get; set; }
        public string name { get; set; }
        public object samples { get; set; }
        public string category { get; set; }
        public Fine_Tuning fine_tuning { get; set; }
        public Labels labels { get; set; }
        public string description { get; set; }
        public string preview_url { get; set; }
        public object[] available_for_tiers { get; set; }
        public VoiceSettings settings { get; set; }
    }

    public sealed class Fine_Tuning
    {
        public object model_id { get; set; }
        public bool is_allowed_to_fine_tune { get; set; }
        public bool fine_tuning_requested { get; set; }
        public string finetuning_state { get; set; }
        public object verification_attempts { get; set; }
        public object[] verification_failures { get; set; }
        public int verification_attempts_count { get; set; }
        public object slice_ids { get; set; }
    }

    public sealed class Labels
    {
    }

    public sealed class VoiceSettings
    {
        public float stability { get; set; } = 0.75f;
        public float similarity_boost { get; set; } = 0.75f;

        public override int GetHashCode()
        {
            return HashCode.Combine(stability, similarity_boost);
        }
    }

    public sealed class TTS_Body
    {
        public string text { get; set; }
        public VoiceSettings voice_settings { get; set; }
    }
}
