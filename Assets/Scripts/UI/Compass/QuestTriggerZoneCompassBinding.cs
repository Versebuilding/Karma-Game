using UnityEngine;

namespace Karma.UI.Compass
{
    /// <summary>
    /// Auto-enables a PrimaryQuest CompassMarker on a waypoint only while
    /// its (questId, objectiveId) is the active objective.
    ///
    /// Attach this next to a QuestTriggerZone (or any GameObject at a
    /// waypoint location) together with a CompassMarker. On a typical
    /// QuestTriggerZone setup the binding reads questId/objectiveId from
    /// the sibling zone automatically; override the inspector fields to
    /// use the binding on an arbitrary waypoint object.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CompassMarker))]
    public class QuestTriggerZoneCompassBinding : MonoBehaviour
    {
        [Header("Overrides (leave empty to read from sibling QuestTriggerZone)")]
        [SerializeField] private string questIdOverride;
        [SerializeField] private string objectiveIdOverride;

        private CompassMarker _marker;
        private string _questId;
        private string _objectiveId;
        private bool _subscribed;

        void Awake()
        {
            _marker = GetComponent<CompassMarker>();
            _marker.type = CompassMarkerType.PrimaryQuest;

            ResolveIds();
        }

        void OnEnable()
        {
            TrySubscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveIds()
        {
            _questId = questIdOverride;
            _objectiveId = objectiveIdOverride;

            if (string.IsNullOrEmpty(_questId) || string.IsNullOrEmpty(_objectiveId))
            {
                var zone = GetComponent<QuestTriggerZone>();
                if (zone != null)
                {
                    if (string.IsNullOrEmpty(_questId)) _questId = zone.QuestId;
                    if (string.IsNullOrEmpty(_objectiveId)) _objectiveId = zone.ObjectiveId;
                }
            }
        }

        private void TrySubscribe()
        {
            if (_subscribed || QuestManager.Instance == null) return;
            QuestManager.Instance.OnQuestStarted += OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;
            QuestManager.Instance.OnQuestCompleted += OnQuestEvent;
            QuestManager.Instance.OnQuestFailed += OnQuestEvent;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || QuestManager.Instance == null) { _subscribed = false; return; }
            QuestManager.Instance.OnQuestStarted -= OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
            QuestManager.Instance.OnQuestCompleted -= OnQuestEvent;
            QuestManager.Instance.OnQuestFailed -= OnQuestEvent;
            _subscribed = false;
        }

        void Start()
        {
            // QuestManager may initialise after us — retry once.
            TrySubscribe();
            Refresh();
        }

        private void OnQuestEvent(string _) => Refresh();
        private void OnObjectiveUpdated(string _, string __, int ___, int ____) => Refresh();

        private void Refresh()
        {
            if (string.IsNullOrEmpty(_questId) || string.IsNullOrEmpty(_objectiveId))
            {
                _marker.enabled = false;
                return;
            }

            bool shouldShow = false;
            var qm = QuestManager.Instance;
            if (qm != null && qm.IsQuestActive(_questId))
            {
                var progress = qm.GetObjectiveProgress(_questId, _objectiveId);
                shouldShow = progress.required > 0 && progress.current < progress.required;
            }

            if (_marker.enabled != shouldShow) _marker.enabled = shouldShow;
        }
    }
}
