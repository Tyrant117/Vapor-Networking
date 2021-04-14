#if STEAMWORKS
using Steamworks;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking.Steam
{
    public class SteamLobbyHelpers
    {
#if STEAMWORKS
        public static T GetLobbyData<T>(CSteamID lobby, string key)
        {
            return JsonUtility.FromJson<T>(SteamMatchmaking.GetLobbyData(lobby, key));
        }

        public static bool SetLobbyData<T>(CSteamID lobby, string key, T value)
        {
            var result = JsonUtility.ToJson(value, false);
            return SteamMatchmaking.SetLobbyData(lobby, key, result);
        }

        public static T GetLobbyMemberData<T>(CSteamID lobby, CSteamID user, string key)
        {
            return JsonUtility.FromJson<T>(SteamMatchmaking.GetLobbyMemberData(lobby, user, key));
        }

        public static void SetLobbyMemberData<T>(CSteamID lobby, string key, T value)
        {
            var result = JsonUtility.ToJson(value, false);
            SteamMatchmaking.SetLobbyMemberData(lobby, key, result);
        }
#endif
    }
}