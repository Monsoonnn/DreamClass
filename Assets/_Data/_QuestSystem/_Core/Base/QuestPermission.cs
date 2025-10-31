using UnityEngine;

namespace DreamClass.QuestSystem {
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    namespace Systems.Quest {
        [DefaultExecutionOrder(-1000)]
        public class QuestPermissionManager : SingletonCtrl<QuestPermissionManager> {
            private readonly HashSet<string> completedQuests = new HashSet<string>();
            protected override void Awake() {
                base.Awake();
                Load();
            }

            public bool HasCompleted( string questId ) => completedQuests.Contains(questId);

            public bool HasAll( IEnumerable<string> questIds ) {
                if (questIds == null || !questIds.Any()) return true;
                return questIds.All(id => completedQuests.Contains(id));
            }

            public void MarkCompleted( string questId ) {
                Debug.Log($"[QuestPermissionManager] Marking quest '{questId}' as completed.");
                if (completedQuests.Add(questId)) Save();
            }

            void Save() {
                string data = string.Join(",", completedQuests);
                PlayerPrefs.SetString("completed_quests", data);
            }

            void Load() {
                if (!PlayerPrefs.HasKey("completed_quests")) return;
                string data = PlayerPrefs.GetString("completed_quests");
                foreach (var id in data.Split(',')) {
                    if (!string.IsNullOrWhiteSpace(id))
                        completedQuests.Add(id);
                }
            }
        }
    }

}
