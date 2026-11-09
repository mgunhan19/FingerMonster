using UnityEngine;

namespace FingerMonster.Audio
{
    /// <summary>
    /// Harici ses dosyası gerektirmeden Unity içinde sıfırdan sentetik ses dalgaları
    /// üreten prosedürel çizgi film ses motoru. Absürt kasap silahları ve komik tepkileri barındırır.
    /// </summary>
    public static class SoundEffectsGenerator
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// Mekanik daktilo tuşuna basılma sesi (kısa, keskin mekanik vuruş).
        /// </summary>
        public static AudioClip CreateTypewriterClickClip()
        {
            int length = (int)(SampleRate * 0.08f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 80f);
                float noise = (Random.value * 2f - 1f) * 0.5f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.5f;
                data[i] = (noise + tone) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("TypewriterClick_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Tuzak kapanma ve el kopma efekti (ani metalik şakırtı ve yay fırlama efekti).
        /// </summary>
        public static AudioClip CreateTrapStingerClip()
        {
            int length = (int)(SampleRate * 0.45f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 12f);
                float freq = Mathf.Lerp(450f, 60f, t / 0.45f);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t);
                // Yay fırlama cıvıltısı (Chirp)
                float springChirp = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 800f, t / 0.45f) * t) * 0.4f;
                float crackle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 40f);
                data[i] = Mathf.Clamp((sub * 0.6f + springChirp + crackle * 0.5f) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("TrapStinger_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Daktilo satır sonu veya tur başlangıç başarı zili (temiz çan tonu).
        /// </summary>
        public static AudioClip CreateBellDingClip()
        {
            int length = (int)(SampleRate * 0.6f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 6f);
                float h1 = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.6f;
                float h2 = Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.3f;
                data[i] = (h1 + h2) * envelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create("BellDing_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Kasap belirdiğinde çalan uğursuz bas gerilim stinger sesi.
        /// </summary>
        public static AudioClip CreateButcherStingerClip()
        {
            int length = (int)(SampleRate * 1.5f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Min(t * 8f, 1f) * Mathf.Exp(-t * 1.5f);
                float d1 = Mathf.Sin(2f * Mathf.PI * 75f * t);
                float d2 = Mathf.Sin(2f * Mathf.PI * 81f * t);
                float rumble = (Random.value * 2f - 1f) * 0.15f;
                data[i] = Mathf.Clamp(((d1 + d2) * 0.45f + rumble) * envelope, -1f, 1f) * 0.8f;
            }

            AudioClip clip = AudioClip.Create("ButcherStinger_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Silah 1: Dev Çizgi Film Tokmağı vuruşu ("BONK!").
        /// </summary>
        public static AudioClip CreateMalletBonkClip()
        {
            int length = (int)(SampleRate * 0.5f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 14f);
                float freq = Mathf.Lerp(280f, 70f, t / 0.5f);
                float thud = Mathf.Sin(2f * Mathf.PI * freq * t);
                float hollow = Mathf.Sin(2f * Mathf.PI * (freq * 2.2f) * t) * 0.5f;
                data[i] = (thud + hollow) * envelope * 0.85f;
            }

            AudioClip clip = AudioClip.Create("MalletBonk_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Silah 2: Uçan Döküm Tava çarpması ("DÖNNNKKK!").
        /// </summary>
        public static AudioClip CreatePanDongClip()
        {
            int length = (int)(SampleRate * 1.2f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 3.5f);
                float clang1 = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.5f;
                float clang2 = Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.35f;
                float clang3 = Mathf.Sin(2f * Mathf.PI * 2350f * t) * 0.2f;
                data[i] = (clang1 + clang2 + clang3) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("PanDong_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Silah 3: Sosis Kırbacı şaplağı ("ŞŞŞRAAAK!").
        /// </summary>
        public static AudioClip CreateWhipSlapClip()
        {
            int length = (int)(SampleRate * 0.35f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 22f);
                float snapNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.8f;
                float slapTone = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.4f;
                data[i] = (snapNoise + slapTone) * envelope * 0.9f;
            }

            AudioClip clip = AudioClip.Create("WhipSlap_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Silah 4: Kremalı Pasta patlaması ("SSSPLLLAATTT!").
        /// </summary>
        public static AudioClip CreatePieSplatClip()
        {
            int length = (int)(SampleRate * 0.45f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 10f);
                float squish = (Random.value * 2f - 1f) * Mathf.Sin(2f * Mathf.PI * 180f * t);
                float lowSlop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 80f, t / 0.45f) * t) * 0.5f;
                data[i] = (squish * 0.6f + lowSlop) * envelope * 0.85f;
            }

            AudioClip clip = AudioClip.Create("PieSplat_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Kasabın arkadan duyulan kısık kıkırdaması ("heh heh heh").
        /// </summary>
        public static AudioClip CreateButcherGiggleClip()
        {
            int length = (int)(SampleRate * 0.9f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                // 3 heceli kıkırdama (heh... heh... heh...)
                float pulse = Mathf.Sin(2f * Mathf.PI * 3.3f * t);
                pulse = Mathf.Clamp01(pulse);

                float pitch = 140f + Mathf.Sin(t * 30f) * 20f;
                float tone = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.5f;
                float breath = (Random.value * 2f - 1f) * 0.2f;

                data[i] = (tone + breath) * pulse * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ButcherGiggle_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Komik ve gerilimli çizgi film pizzicato bas müziği döngüsü.
        /// </summary>
        public static AudioClip CreateCartoonSuspenseMusicLoop()
        {
            float bpm = 110f;
            float beatDuration = 60f / bpm;
            int totalBeats = 16;
            int length = (int)(SampleRate * (beatDuration * totalBeats));
            float[] data = new float[length];

            int[] melodyNotes = new int[] { 48, 51, 53, 54, 53, 51, 48, 46, 48, 51, 55, 54, 51, 48, 50, 48 };

            for (int beat = 0; beat < totalBeats; beat++)
            {
                int startSample = (int)(beat * beatDuration * SampleRate);
                int noteSamples = (int)(beatDuration * SampleRate);
                float freq = 440f * Mathf.Pow(2f, (melodyNotes[beat] - 69) / 12f);

                for (int s = 0; s < noteSamples && (startSample + s) < length; s++)
                {
                    float t = (float)s / SampleRate;
                    float env = Mathf.Exp(-t * 9f); // Pizzicato staccato düşüş
                    float note = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.25f;
                    data[startSample + s] = note * env * 0.25f;
                }
            }

            AudioClip clip = AudioClip.Create("CartoonSuspenseMusic_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateUIClickClip()
        {
            int length = (int)(SampleRate * 0.04f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 120f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("UIClick_Proc", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
