using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetaXRHeadBlocking : MonoBehaviour {

    [SerializeField] public GameObject player = null;
    [SerializeField] private LayerMask _collisionLayers = 1 << 0;
    [SerializeField] private float _collisionRadius = 0.2f;
    [SerializeField] private float afkThreshold = 480f;

    private Vector3 prevHeadPos;
    private Vector3 lastActivityPosition;
    private Quaternion lastActivityRotation;
    private bool isAfk = false;

    private void Start() {
        prevHeadPos = transform.position;
        if (player != null) {
            lastActivityPosition = player.transform.position;
            lastActivityRotation = player.transform.rotation;
            ScheduleAfkCheck();
        }
    }

    private void ScheduleAfkCheck() {
        CancelInvoke(nameof(OnAfkDetected));
        Invoke(nameof(OnAfkDetected), afkThreshold);
    }

    private void OnAfkDetected() {
        if (player == null) return;

       /* Debug.Log("Checking if player is AFK");*/

        bool positionUnchanged = player.transform.position == lastActivityPosition;
        bool rotationUnchanged = player.transform.rotation == lastActivityRotation;

        if (positionUnchanged && rotationUnchanged) {
            isAfk = true;
            _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.afk);

            Debug.Log("Player is AFK");
        }
    }

    private void DetectPlayerActivity() {
        if (player == null) return;

        if (player.transform.position != lastActivityPosition ||
            player.transform.rotation != lastActivityRotation) {

            if (isAfk) {
                Debug.Log("Player returned from AFK");
                isAfk = false;
            }

            lastActivityPosition = player.transform.position;
            lastActivityRotation = player.transform.rotation;

            ScheduleAfkCheck();
        }
    }

    private void Update() {
        if (player == null) return;

        DetectPlayerActivity();

        bool collision = DetectHit(transform.position);
        if (!collision) {
            prevHeadPos = transform.position;
        } else {
            Vector3 headDiff = transform.position - prevHeadPos;
            Vector3 adjHeadPos = new Vector3(player.transform.position.x - headDiff.x,
                                             player.transform.position.y,
                                             player.transform.position.z - headDiff.z);
            player.transform.SetPositionAndRotation(adjHeadPos, player.transform.rotation);
        }
    }

    private bool DetectHit( Vector3 loc ) {
        Collider[] objs = new Collider[10];
        int size = Physics.OverlapSphereNonAlloc(loc, _collisionRadius, objs,
                   _collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < size; i++) {
            if (objs[i].tag != "Player") {
                return true;
            }
        }
        return false;
    }

    public bool IsAfk() {
        return isAfk;
    }
}
