#nullable enable
// The save text in PlayerPrefs (paket V1; WebGL: IndexedDB, Android: SharedPreferences). `Save()` after every write: WebGL
// flushes PlayerPrefs only on Save or a clean quit, and a phone kill is never clean.
using MinikUsta.Presentation.Save;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class PlayerPrefsStore : ISaveStore
    {
        public string? Read(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void Write(string key, string text)
        {
            PlayerPrefs.SetString(key, text);
            PlayerPrefs.Save();
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
