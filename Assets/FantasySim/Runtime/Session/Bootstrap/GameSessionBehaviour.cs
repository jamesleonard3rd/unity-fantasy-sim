using System.Collections;
using UnityEngine;

namespace FantasySim.Session
{
    public sealed class GameSessionBehaviour : MonoBehaviour, ITimeSource
    {
        [Tooltip("Real seconds between scheduler polls. The scheduler ticks at most one due partition per poll.")]
        [SerializeField]
        private float schedulerPollSeconds = 1f;

        [Tooltip("Log a message whenever a coarse partition ticks.")]
        [SerializeField]
        private bool logPartitionTicks = true;

        private GameSessionService session;

        public double NowSeconds => Time.timeAsDouble;

        private void OnEnable()
        {
            session = new GameSessionService(this);
            session.StartSession();
            StartCoroutine(RunScheduler());
        }

        private void OnDisable()
        {
            session?.StopSession();
            session = null;
        }

        private IEnumerator RunScheduler()
        {
            var wait = new WaitForSeconds(Mathf.Max(0f, schedulerPollSeconds));
            while (session != null && session.IsRunning)
            {
                PartitionTickReport report = session.Update();
                if (report != null && logPartitionTicks)
                {
                    Debug.Log(
                        $"Coarse tick partition {report.PartitionId}: " +
                        $"tick {report.PreviousTick.Value} -> {report.CurrentTick.Value}, " +
                        $"{report.EventsInserted} events, {report.ElapsedMilliseconds:F1} ms."
                    );
                }

                yield return wait;
            }
        }
    }
}
