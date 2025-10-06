using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JunEx
{
    public static class PlayerPrefsExtension
    {
        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        public static void SetDateTime(string key, DateTime value)
        {
            var json = JsonUtility.ToJson((JsonDateTime)value);
            PlayerPrefs.SetString(key, json);
            DateTime test = JsonUtility.FromJson<JsonDateTime>(json);
        }
        public static DateTime GetDateTime(string key, DateTime defaultValue)
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }
            DateTime value = JsonUtility.FromJson<JsonDateTime>(json);
            return value;
        }
    }
    [Serializable]
    struct JsonDateTime
    {
        public long value;
        public static implicit operator DateTime(JsonDateTime jdt)
        {
            return DateTime.FromBinary(jdt.value);
        }
        public static implicit operator JsonDateTime(DateTime dt)
        {
            JsonDateTime jdt = new JsonDateTime();
            jdt.value = dt.ToBinary();
            return jdt;
        }
    }
}