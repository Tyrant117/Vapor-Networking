#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace VaporNetworking
{
    public class NetObserverUtility : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [BoxGroup("Network Info"), SuffixLabel("ms"), ReadOnly]
#endif
        [SerializeField]
        private double ping;
#if ODIN_INSPECTOR
        [BoxGroup("Network Info"), SuffixLabel("kb/s"), ReadOnly]
#endif
        [SerializeField]
        private float outRate;
#if ODIN_INSPECTOR
        [BoxGroup("Network Info"), SuffixLabel("kb/s"), ReadOnly]
#endif
        [SerializeField]
        private float inRate;

        public bool observe;

        private void Awake()
        {
            if (!observe)
            {
                Destroy(gameObject);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            ping = ServerTime.Rtt * 0.5f;
            if (NetLogFilter.messageDiagnostics)
            {
                NetDiagnostics.AverageThroughput(Time.time);
                outRate = NetDiagnostics.aveBytesOut / 1000f;
                inRate = NetDiagnostics.aveBytesIn / 1000f;
            }
        }
    }
}
