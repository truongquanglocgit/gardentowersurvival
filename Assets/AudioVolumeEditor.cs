#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Diagnostics;

namespace JunEx
{
    public class AudioVolumeEditor : EditorWindow
    {
        private AudioClip mAudioClip; // Selected audioclip
        private float mTargetDb = -3.0f; // Target decibel value
        private float mMaxDb; // audioclip's max db

        private Texture2D mListenBtnTexture;

        private string mScriptFoliderPath;
        private string mBackupFolderPath;


        [MenuItem("Tools/JunEx/Audio Volume Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioVolumeEditor>("Audio Volume Editor");
            window.minSize = new Vector2(300, 210);
            window.maxSize = new Vector2(300, 210);
        }

        private void OnEnable()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            mScriptFoliderPath = Path.GetDirectoryName(scriptPath);

            string imagePath = $"{mScriptFoliderPath}/Images/ListenButtonImage.png";
            mListenBtnTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);

            // Create the backup folder path
            mBackupFolderPath = Path.Combine(mScriptFoliderPath, "_Backups");
        }

        private void OnGUI()
        {
            // Create the backup folder if it doesn't exist
            if (!Directory.Exists(mBackupFolderPath))
            {
                Directory.CreateDirectory(mBackupFolderPath);
                AssetDatabase.Refresh();

                Log($"Backup Folder Created at: {mBackupFolderPath}");
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Audio File");

            // Find an AudioClip among the selected objects in the Project window
            AudioClip selectedAudioClip = null;
            System.Object[] selectedObjects = Selection.objects;
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                foreach (var selectedObject in selectedObjects)
                {
                    if (selectedObject is AudioClip)
                    {
                        selectedAudioClip = selectedObject as AudioClip;
                        break; // Use the first found AudioClip
                    }
                }
            }

            // Disable direct object selection in the Project window
            EditorGUI.BeginDisabledGroup(true);
            selectedAudioClip = EditorGUILayout.ObjectField("", selectedAudioClip, typeof(AudioClip), false, GUILayout.Width(180)) as AudioClip;
            EditorGUI.EndDisabledGroup();

            // Update the audioClip
            mAudioClip = selectedAudioClip;

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // Left alignment
            GUILayout.Label("Target Decibel ");

            // Right alignment
            GUILayout.FlexibleSpace(); // Add right margin
            GUILayout.Label("-");
            mTargetDb = EditorGUILayout.FloatField("", mTargetDb, GUILayout.Width(126));
            mTargetDb = Mathf.Clamp(mTargetDb, 0f, 80f);
            GUILayout.Label("(dB)");

            if (GUILayout.Button(mListenBtnTexture, GUILayout.Width(20), GUILayout.Height(20)))
            {
                ListenToAudio();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Process and Replace"))
            {
                if (mAudioClip == null)
                {
                    Log("Please select a Audio file.");
                }
                else
                {
                    (float[] samples, int channels, int sampleRate) = ProcessAndExport();

                    // Get the AudioClip's path
                    string audioClipPath = AssetDatabase.GetAssetPath(mAudioClip);

                    // Backup the original file
                    if (File.Exists(audioClipPath))
                    {
                        string backupDir = Path.Combine(mScriptFoliderPath, "_Backups");
                        Directory.CreateDirectory(backupDir); // Ensure backup folder exists

                        string backupPath = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(audioClipPath));
                        string extension = Path.GetExtension(audioClipPath);
                        string timestamp = System.DateTime.Now.ToString("_yyMMdd_HHmmss");

                        File.Copy(audioClipPath, $"{backupPath}_bak{timestamp}{extension}", true);

                        File.Delete(audioClipPath);
                    }

                    // Convert to WAV (in-memory) and then to OGG via FFmpeg
                    byte[] wavBytes = SaveWavToBytes(samples, channels, sampleRate);

                    // Overwrite original with .ogg
                    string outputOggPath = Path.ChangeExtension(audioClipPath, ".ogg");
                    ConvertWavBytesToOgg(wavBytes, outputOggPath);

                    // Refresh
                    AssetDatabase.Refresh();

                    Log("🎵 Audio has been converted and saved as .ogg");
                }
            }
            if (GUILayout.Button("Process and Export"))
            {
                if (mAudioClip == null)
                {
                    Log("Please select a Audio file.");
                }
                else
                {
                    (float[] samples, int channels, int sampleRate) = ProcessAndExport();

                    // Get the file path
                    string outputPath = EditorUtility.SaveFilePanel("Save Processed WAV", "", "processed.wav", "wav");
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        // Save as a WAV file
                        SaveWav(outputPath, samples, channels, sampleRate);

                        Log($".WAV Asset saved at {outputPath}.");
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Add Detail Info section
            GUILayout.Label("[Audio Clip Detail Info]");
            if (mAudioClip == null)
            {
                GUILayout.Label($"Select a WAV File from the Project!");
            }
            else
            {
                GUILayout.Label($"Max DB: {GetMaxDB()}");
                GUILayout.Label($"Sample Rate: {mAudioClip.frequency} Hz"); // Sample rate information
                GUILayout.Label($"Channels: {mAudioClip.channels}"); // Channel information
                GUILayout.Label($"Length: {mAudioClip.length:F2} seconds"); // Audio clip length
            }

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Powered by: Bonnate");

            if (GUILayout.Button("Github", GetHyperlinkLabelStyle()))
            {
                OpenURL("https://github.com/bonnate");
            }

            if (GUILayout.Button("Blog", GetHyperlinkLabelStyle()))
            {
                OpenURL("https://bonnate.tistory.com/");
            }

            GUILayout.EndHorizontal();
        }

        private float GetMaxDB()
        {
            if (mAudioClip == null)
            {
                return 0f;
            }

            float[] samples = new float[mAudioClip.samples * mAudioClip.channels];
            mAudioClip.GetData(samples, 0);

            // Find the highest decibel value
            mMaxDb = -Mathf.Infinity;
            const float epsilon = 1e-10f;
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = Mathf.Abs(samples[i]);
                float db = 20.0f * Mathf.Log10(Mathf.Max(sample, epsilon));
                if (db > mMaxDb)
                {
                    mMaxDb = db;
                }
            }

            return mMaxDb;
        }

        private (float[], int, int) ProcessAndExport()
        {
            int sampleRate = mAudioClip.frequency;
            int channels = mAudioClip.channels; // Get the channel count from the original WAV file

            float[] samples = new float[mAudioClip.samples * channels];
            mAudioClip.GetData(samples, 0);

            // Calculate the multiplier to adjust the decibel value
            float dbDifference = (-mTargetDb) - mMaxDb;
            float multiplier = Mathf.Pow(10.0f, dbDifference / 20.0f);

            // Adjust the decibel value by multiplying the multiplier to all samples
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= multiplier;
            }

            // Export as WAV format
            return (samples, channels, sampleRate);
        }

        private byte[] SaveWavToBytes(float[] samples, int channels, int sampleRate)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                int byteRate = sampleRate * channels * 2;
                int subChunk2Size = samples.Length * 2;
                int chunkSize = 36 + subChunk2Size;

                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(chunkSize);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);

                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(subChunk2Size);

                foreach (float sample in samples)
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767f);
                    writer.Write(intSample);
                }

                writer.Flush();
                return memoryStream.ToArray();
            }
        }

        private void ConvertWavBytesToOgg(byte[] wavData, string outputOggPath)
        {
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "FFmpeg/ffmpeg.exe");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-f wav -i pipe:0 -c:a libvorbis -qscale:a 5 \"{outputOggPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Ghi dữ liệu WAV vào stdin
                using (BinaryWriter stdin = new BinaryWriter(process.StandardInput.BaseStream))
                {
                    stdin.Write(wavData);
                }

                // Đọc log lỗi nếu có
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (File.Exists(outputOggPath))
                {
                    UnityEngine.Debug.Log($"✅ OGG file saved: {outputOggPath}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"❌ FFmpeg failed: {error}");
                }
            }
        }



        // Function to save WAV files
        private void SaveWav(string path, float[] samples, int channels, int sampleRate)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                // Write the WAV header
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Length * 2);
                writer.Write(new char[8] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2 * channels);
                writer.Write((ushort)(2 * channels));
                writer.Write((ushort)16);
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Length * 2);

                // Write WAV data
                foreach (float sample in samples)
                {
                    writer.Write((short)(sample * 32767.0f));
                }
            }
        }

        private void ListenToAudio()
        {
            if (mAudioClip == null)
            {
                Log("Please select a WAV file.");
                return;
            }

            AudioSource audioSource = EditorUtility.CreateGameObjectWithHideFlags("AudioSource", HideFlags.HideAndDontSave, typeof(AudioSource)).GetComponent<AudioSource>();
            audioSource.clip = mAudioClip;

            if (-mTargetDb < mMaxDb)
            {
                // Adjust the volume of the audio source to listen at the specified mTargetDb.
                float maxVolume = Mathf.Pow(10.0f, (-mTargetDb) / 20.0f);
                audioSource.volume = maxVolume;

                audioSource.Play();
            }
            else
            {
                // Retrieve WAV file data using the ProcessAndExport function
                (float[] samples, int channels, int sampleRate) = ProcessAndExport();

                // Convert and play the WAV file as an AudioClip
                AudioClip tempAudioClip = AudioClip.Create("ProcessedAudioClip", samples.Length / channels, channels, sampleRate, false);
                tempAudioClip.SetData(samples, 0);
                audioSource.clip = tempAudioClip;
                audioSource.Play();
            }
        }

        #region _HYPERLINK
        private GUIStyle GetHyperlinkLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = new Color(0f, 0.5f, 1f);
            style.stretchWidth = false;
            style.wordWrap = false;
            return style;
        }

        private void OpenURL(string url)
        {
            EditorUtility.OpenWithDefaultApp(url);
        }
        #endregion

        #region 
        private void Log(string content)
        {
            UnityEngine.Debug.Log($"<color=cyan>[WAV Easy Volume Editor]</color> {content}");
        }
        #endregion
    }
}
#endif