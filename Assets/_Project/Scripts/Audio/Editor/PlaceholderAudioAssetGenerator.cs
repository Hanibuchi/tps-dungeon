using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Audio.Editor
{
    /// <summary>
    /// 本物の音が用意できるまでの仮アセットを作るエディタ専用ツール。
    /// 16bit PCM の WAV を合成して書き出し、インポート設定まで入れる。
    /// PlaceholderMapAssetGenerator と同じ「あとで差し替える前提の仮物」。
    /// </summary>
    public static class PlaceholderAudioAssetGenerator
    {
        public const string OutputFolder = "Assets/_Project/Audio/Placeholder";

        public const string BgmPath = OutputFolder + "/BGM_Placeholder_Loop.wav";

        /// <summary>
        /// 2 本目の仮 BGM。PlayBgm は同じクリップを渡されると何もしないので、
        /// 1 本だけだとクロスフェードの経路を一度も鳴らして確かめられない。
        /// </summary>
        public const string BgmBPath = OutputFolder + "/BGM_Placeholder_Loop_B.wav";
        public const string ClickPath = OutputFolder + "/SE_Click.wav";
        public const string HitPath = OutputFolder + "/SE_Hit.wav";
        public const string FootstepPath = OutputFolder + "/SE_Footstep.wav";

        private const int SampleRate = 44100;

        [MenuItem("Tools/TPS Dungeon/Audio/Generate Placeholder Audio Clips")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>仮の音を書き出して、何を作ったかのログを返す。</summary>
        public static string Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            WriteWav(BgmPath, BuildBgmLoop(BgmNotesA, droneHz: 55f));
            WriteWav(BgmBPath, BuildBgmLoop(BgmNotesB, droneHz: 65.41f));
            WriteWav(ClickPath, BuildClick());
            WriteWav(HitPath, BuildHit());
            WriteWav(FootstepPath, BuildFootstep());

            AssetDatabase.Refresh();

            ApplyBgmImportSettings(BgmPath);
            ApplyBgmImportSettings(BgmBPath);
            foreach (string path in new[] { ClickPath, HitPath, FootstepPath })
            {
                ApplySeImportSettings(path);
            }

            return "仮の音を生成した:\n  " + string.Join("\n  ", BgmPath, BgmBPath, ClickPath, HitPath, FootstepPath);
        }

        // ---- 波形づくり ------------------------------------------------------

        /// <summary>イ短調のアルペジオ。低い音域に寄せてダンジョンらしい重さを出す。</summary>
        private static readonly float[] BgmNotesA =
            { 110.00f, 130.81f, 164.81f, 220.00f, 164.81f, 130.81f, 110.00f, 98.00f };

        /// <summary>
        /// 2 本目。完全五度ほど上に寄せた別調で、切り替わったことが一聴して分かるようにする。
        /// </summary>
        private static readonly float[] BgmNotesB =
            { 164.81f, 196.00f, 246.94f, 329.63f, 246.94f, 196.00f, 164.81f, 146.83f };

        /// <summary>
        /// 渡した音階のアルペジオが 8 秒で一周するループ。
        /// 端が無音になるようにして、繰り返してもブツッと鳴らないようにしてある。
        /// </summary>
        private static float[] BuildBgmLoop(float[] notes, float droneHz)
        {
            const float seconds = 8f;
            const float noteSeconds = 0.5f;

            int total = (int)(seconds * SampleRate);
            var samples = new float[total];

            for (int i = 0; i < total; i++)
            {
                float time = i / (float)SampleRate;

                int noteIndex = (int)(time / noteSeconds) % notes.Length;
                float noteTime = time % noteSeconds;
                float envelope = Mathf.Exp(-noteTime * 4f) * (1f - Mathf.Exp(-noteTime * 400f));
                float note = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * time) * envelope * 0.35f;

                // 下に薄く敷くドローン。うねりを付けて単調さを消す。
                float drone = Mathf.Sin(2f * Mathf.PI * droneHz * time) * 0.12f
                              * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.125f * time));

                samples[i] = (note + drone) * LoopEdgeFade(time, seconds);
            }

            return samples;
        }

        /// <summary>ループの継ぎ目だけ薄くフェードさせる。</summary>
        private static float LoopEdgeFade(float time, float seconds)
        {
            const float fade = 0.05f;
            if (time < fade) return time / fade;
            if (time > seconds - fade) return (seconds - time) / fade;
            return 1f;
        }

        /// <summary>UI 向けの短いクリック。</summary>
        private static float[] BuildClick()
        {
            return Build(0.06f, (time, random) =>
            {
                float envelope = Mathf.Exp(-time * 90f);
                return Mathf.Sin(2f * Mathf.PI * 1800f * time) * envelope * 0.5f;
            });
        }

        /// <summary>着弾や被弾に使う、低音のアタックとノイズを混ぜたヒット音。</summary>
        private static float[] BuildHit()
        {
            return Build(0.35f, (time, random) =>
            {
                float thump = Mathf.Sin(2f * Mathf.PI * (90f - 50f * time) * time) * Mathf.Exp(-time * 12f) * 0.6f;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-time * 30f) * 0.35f;
                return thump + noise;
            });
        }

        /// <summary>足音。短いノイズのバースト。</summary>
        private static float[] BuildFootstep()
        {
            return Build(0.15f, (time, random) =>
            {
                float envelope = Mathf.Exp(-time * 35f) * (1f - Mathf.Exp(-time * 600f));
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float body = Mathf.Sin(2f * Mathf.PI * 160f * time) * 0.3f;
                return (noise * 0.45f + body) * envelope;
            });
        }

        /// <summary>秒数と 1 サンプル分の式から波形を作る。乱数は毎回同じ結果になるよう固定シード。</summary>
        private static float[] Build(float seconds, Func<float, System.Random, float> shape)
        {
            var random = new System.Random(20260921);
            int total = (int)(seconds * SampleRate);
            var samples = new float[total];

            for (int i = 0; i < total; i++)
            {
                samples[i] = shape(i / (float)SampleRate, random);
            }

            return samples;
        }

        // ---- 書き出し --------------------------------------------------------

        /// <summary>モノラル 16bit PCM の WAV として書き出す。</summary>
        private static void WriteWav(string path, float[] samples)
        {
            const int channels = 1;
            const int bitsPerSample = 16;
            int dataBytes = samples.Length * sizeof(short);

            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });

                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);                                        // fmt チャンクの長さ
                writer.Write((short)1);                                  // PCM
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * channels * bitsPerSample / 8); // バイト毎秒
                writer.Write((short)(channels * bitsPerSample / 8));     // ブロックサイズ
                writer.Write((short)bitsPerSample);

                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (float sample in samples)
                {
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
                }
            }
        }

        // ---- インポート設定 --------------------------------------------------

        /// <summary>BGM は長いのでメモリに展開せずストリーミングで流す。</summary>
        private static void ApplyBgmImportSettings(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;

            // ストリーミングは初回再生でメインスレッドを止めうるので、必ず裏で読ませる。
            importer.loadInBackground = true;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }

        /// <summary>SE は短いので展開済みで持ち、3D で鳴らせるようモノラルに寄せる。</summary>
        private static void ApplySeImportSettings(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;

            importer.loadInBackground = false;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }
}
