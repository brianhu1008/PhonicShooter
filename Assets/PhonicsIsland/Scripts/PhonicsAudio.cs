// ============================================================================
//  Phonics Island - voice and sound effects
//
//  VOICE  Order of preference:
//          1) an AudioClip at Assets/Resources/Voice/<key>.wav|mp3|ogg
//             (key = the text in lower case, spaces as _, other symbols removed,
//              e.g. "Find the shell!" -> find_the_shell)
//          2) on Windows, the built-in English text-to-speech voice. One hidden
//             PowerShell process is kept running so speech starts quickly.
//         Text in {text:ipa} is spoken as an IPA sound, e.g. {sh:ʃ}.
//         Everything is also shown on screen, so the game never gets stuck.
//  SFX    Kenney sound packs (CC0), in Assets/Resources/Sfx.
// ============================================================================
using System.Text;
using UnityEngine;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        AudioSource voiceSrc, sfxSrc;
        AudioClip sfxDing, sfxOops, sfxBoing, sfxPop, sfxWin, sfxThrow, sfxTick, sfxClick;

        void InitAudio()
        {
            voiceSrc = gameObject.AddComponent<AudioSource>();
            voiceSrc.playOnAwake = false;
            sfxSrc = gameObject.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.volume = 0.7f;
            BuildSfx();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            StartTts();
#endif
        }

        void PlaySfx(AudioClip c)
        {
            if (c != null) sfxSrc.PlayOneShot(c);
        }

        // Text shown on screen: "{sh:ʃ}" -> "sh"
        static string PlainText(string text)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('}', open);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string inner = text.Substring(open + 1, close - open - 1);
                int colon = inner.IndexOf(':');
                sb.Append(colon >= 0 ? inner.Substring(0, colon) : inner);
                i = close + 1;
            }
            return sb.ToString();
        }

        static string MakeKey(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in PlainText(text).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            AudioClip clip = Resources.Load<AudioClip>("Voice/" + MakeKey(text));
            if (clip != null)
            {
                voiceSrc.Stop();
                voiceSrc.clip = clip;
                voiceSrc.Play();
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            PhonicsSpeak(WebSpeechText(text));
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            SpeakWindows(text);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser speech (Plugins/WebGL/PhonicsSpeech.jslib). It has no IPA support,
        // so single sounds are written the way an English voice reads them.
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void PhonicsSpeak(string text);

        static readonly System.Collections.Generic.Dictionary<string, string> IpaSpelling =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "æ", "aah" }, { "ɛ", "eh" }, { "ɪ", "ih" }, { "ɑ", "ah" }, { "ʌ", "uh" },
                // Spelled so voices say the combined sound, never the letter names ("ch" alone is read "C H").
                { "ʃ", "shh" }, { "tʃ", "chuh" }, { "θ", "thuh" }, { "tʃə", "chuh" }, { "θə", "thuh" }, { "i", "ee" }, { "eɪ", "ay" }, { "w", "wuh" },
                { "u", "ooh" }, { "ʊ", "uh" }, { "aʊ", "ow" }, { "ɔɪ", "oy" }
            };

        static string WebSpeechText(string text)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open);
                if (open < 0 || close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string inner = text.Substring(open + 1, close - open - 1);
                int colon = inner.IndexOf(':');
                string spoken;
                if (colon < 0) spoken = inner;
                else if (!IpaSpelling.TryGetValue(inner.Substring(colon + 1), out spoken)) spoken = inner.Substring(0, colon);
                sb.Append(spoken);
                i = close + 1;
            }
            return sb.ToString();
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        System.Diagnostics.Process ttsProc;

        // One line per utterance, base64 UTF-8. Parts are split by \u0001; a part starting
        // with \u0002 is "text\u0003ipa" and is spoken with that pronunciation.
        const string TtsScript =
            "Add-Type -AssemblyName System.Speech\n" +
            "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer\n" +
            "$v = $s.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -like 'en-*' } | Select-Object -First 1\n" +
            "if (-not $v) { exit }\n" +
            "$s.SelectVoice($v.VoiceInfo.Name)\n" +
            "$s.Rate = -2\n" +
            "while ($true) {\n" +
            "  $line = [Console]::In.ReadLine()\n" +
            "  if ($line -eq $null) { break }\n" +
            "  $s.SpeakAsyncCancelAll()\n" +
            "  try {\n" +
            "    $txt = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($line))\n" +
            "    $pb = New-Object System.Speech.Synthesis.PromptBuilder\n" +
            "    foreach ($part in $txt.Split([char]1)) {\n" +
            "      if ($part.Length -eq 0) { continue }\n" +
            "      if ($part[0] -eq [char]2) { $p = $part.Substring(1).Split([char]3); $pb.AppendTextWithPronunciation($p[0], $p[1]) }\n" +
            "      else { $pb.AppendText($part) }\n" +
            "    }\n" +
            "    $s.SpeakAsync($pb) | Out-Null\n" +
            "  } catch { }\n" +
            "}\n";

        void StartTts()
        {
            try
            {
                string encoded = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(TtsScript));
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                ttsProc = System.Diagnostics.Process.Start(psi);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PhonicsIsland] Text-to-speech not available: " + e.Message);
                ttsProc = null;
            }
        }

        void SpeakWindows(string text)
        {
            if (ttsProc == null || ttsProc.HasExited) return;
            var sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open);
                if (open < 0 || close < 0) { sb.Append('\u0001').Append(text, i, text.Length - i); break; }
                if (open > i) sb.Append('\u0001').Append(text, i, open - i);
                string inner = text.Substring(open + 1, close - open - 1);
                int colon = inner.IndexOf(':');
                if (colon >= 0) sb.Append('\u0001').Append('\u0002').Append(inner.Substring(0, colon)).Append('\u0003').Append(inner.Substring(colon + 1));
                else sb.Append('\u0001').Append(inner);
                i = close + 1;
            }
            try
            {
                ttsProc.StandardInput.WriteLine(System.Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString())));
                ttsProc.StandardInput.Flush();
            }
            catch { }
        }
#endif

        void StopTts()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try { if (ttsProc != null && !ttsProc.HasExited) ttsProc.Kill(); } catch { }
            ttsProc = null;
#endif
        }

        void OnApplicationQuit()
        {
            StopTts();
        }

        // ------------------------------------------------------------------
        //  Sound effects (Kenney, CC0 - see Resources/Sfx/LICENSE.txt)
        // ------------------------------------------------------------------
        void BuildSfx()
        {
            sfxDing = Resources.Load<AudioClip>("Sfx/correct");
            sfxOops = Resources.Load<AudioClip>("Sfx/oops");
            sfxBoing = Resources.Load<AudioClip>("Sfx/bonk");
            sfxThrow = Resources.Load<AudioClip>("Sfx/throw");
            sfxPop = Resources.Load<AudioClip>("Sfx/hit");
            sfxTick = Resources.Load<AudioClip>("Sfx/tile");
            sfxClick = Resources.Load<AudioClip>("Sfx/click");
            sfxWin = Resources.Load<AudioClip>("Sfx/win");
        }
    }
}