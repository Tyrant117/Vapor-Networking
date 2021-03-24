using UnityEngine;

namespace VaporNetworking
{
    public class TransportStatisticsViewer : MonoBehaviour
    {
        public bool view;

        private void OnGUI()
        {
            if (!view) return;
            UDPTransport.OnLogStatistics(false, out var server, out var client);

            GUILayout.BeginArea(new Rect(8, 8, 300, 600));

            GUILayout.BeginVertical("Box");
            GUILayout.Label("SERVER");
            GUILayout.Label(server);
            GUILayout.Space(12);
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Box");
            GUILayout.Label("CLIENT");
            GUILayout.Label(client);
            GUILayout.Space(12);
            GUILayout.EndVertical();

            GUILayout.EndArea();
        }
    }
}