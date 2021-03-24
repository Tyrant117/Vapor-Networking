using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public class CallbackTimer : MonoBehaviour
    {
        public static long CurrentTick { get; protected set; }
        public delegate void DoneHandler(bool isSuccessful);

        private static CallbackTimer instance;
        public static CallbackTimer Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("CallbackTimer");
                    instance = go.AddComponent<CallbackTimer>();
                }
                return instance;
            }
        }

        private List<Action> mainThreadActions;

        /// <summary>
        /// Event, which is invoked every second
        /// </summary>
        public event Action<long> OnTick;

        private readonly object mainThreadLock = new object();

        private void Awake()
        {
            // Framework requires applications to run in background
            Application.runInBackground = true;

            mainThreadActions = new List<Action>();
            instance = this;
            DontDestroyOnLoad(this);

            StartCoroutine(StartTicker());
        }

        private void Update()
        {
            if (mainThreadActions.Count > 0)
            {
                lock (mainThreadLock)
                {
                    foreach (var actions in mainThreadActions)
                    {
                        actions.Invoke();
                    }

                    mainThreadActions.Clear();
                }
            }
        }

        /// <summary>
        ///     Waits while condition is false
        ///     If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="doneCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitUntil(Func<bool> condition, DoneHandler doneCallback, float timeoutSeconds)
        {
            Instance.StartCoroutine(WaitWhileTrueCoroutine(condition, doneCallback, timeoutSeconds, true));
        }

        /// <summary>
        ///     Waits while condition is true
        ///     If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="doneCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitWhile(Func<bool> condition, DoneHandler doneCallback, float timeoutSeconds)
        {
            Instance.StartCoroutine(WaitWhileTrueCoroutine(condition, doneCallback, timeoutSeconds));
        }

        private static IEnumerator WaitWhileTrueCoroutine(Func<bool> condition, DoneHandler callback, float timeoutSeconds, bool reverseCondition = false)
        {
            while ((timeoutSeconds > 0) && (condition.Invoke() == !reverseCondition))
            {
                timeoutSeconds -= Time.deltaTime;
                yield return null;
            }

            callback.Invoke(timeoutSeconds > 0);
        }

        /// <summary>
        /// Invokes callback after waiting set number seconds.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        public static void AfterSeconds(float time, Action callback)
        {
            Instance.StartCoroutine(Instance.StartWaitingSeconds(time, callback));
        }

        /// <summary>
        ///     Executes once on the update call of the main thread.
        /// </summary>
        /// <param name="action"></param>
        public static void ExecuteOnMainThread(Action action)
        {
            Instance.OnMainThread(action);
        }

        public void OnMainThread(Action action)
        {
            lock (mainThreadLock)
            {
                mainThreadActions.Add(action);
            }
        }

        private IEnumerator StartWaitingSeconds(float time, Action callback)
        {
            yield return new WaitForSeconds(time);
            callback?.Invoke();
        }

        private IEnumerator StartTicker()
        {
            CurrentTick = 0;
            while (true)
            {
                yield return new WaitForSeconds(1);
                CurrentTick++;
                OnTick?.Invoke(CurrentTick);
            }
        }
    }
}